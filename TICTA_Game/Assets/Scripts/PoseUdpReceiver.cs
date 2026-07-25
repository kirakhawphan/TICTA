using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// ---------------------------------------------------------------------------------------------
// Wire schema produced by game_bridge.py (see UNITY_INTEGRATION.md).
// Field names must match the JSON keys exactly for JsonUtility.
// ---------------------------------------------------------------------------------------------
[Serializable] public class PoseHeader { public string type; public int seq; public long t_ms; }

[Serializable] public class PoseSidePair { public float left; public float right; }

[Serializable] public class PoseArmedState {
    public bool punch_left, punch_right, elbow_left, elbow_right, kick_left, kick_right;
}

[Serializable] public class PoseLimbOk { public bool arm_left, arm_right, leg_left, leg_right; }

/// <summary>Continuous whole-body signal for 1:1 avatar mapping (see PoseBodyDriver).</summary>
[Serializable] public class PoseBody {
    public float center_x;   // -1 (frame left) .. +1 (frame right), mid-hip
    public float center_y;   // -1 (top) .. +1 (bottom)
    public float lean;       // SIGNED trunk lean degrees (+ = toward +x on screen)
    public float scale;      // torso length / frame height -> distance-to-camera proxy
}

[Serializable] public class PoseStatePayload {
    public bool pose_detected;
    public PoseSidePair shoulder_abduction;
    public PoseSidePair elbow;
    public PoseSidePair hip_abduction;
    public float alpha;                 // trunk lean magnitude (compensation)
    public float beta;                  // shoulder hike (reserved, 0 for now)
    public PoseArmedState armed;
    public PoseLimbOk limb_ok;
    public PoseBody body;
}

[Serializable] public class PoseEventPayload {
    public int event_id;
    public string move;                 // punch_left | punch_right | elbow_* | kick_*
    public string side;
    public float peak_rom;
    public float alpha;
    public float beta;
    public string quality;              // perfect | good | poor
}

[Serializable] public class PoseStateMessage {
    public string type; public int seq; public long t_ms; public PoseStatePayload payload;
}

[Serializable] public class PoseEventMessage {
    public string type; public int seq; public long t_ms; public PoseEventPayload payload;
}

[Serializable] public class MoveAttackBinding {
    public string move;         // pose move from the bridge
    public string attackName;   // must match PlayerAttack.attackName in PlayerMovement
}

/// <summary>
/// Receives pose "move" events from game_bridge.py over UDP and makes the fighter throw the
/// matching strike. Also exposes the continuous body state for PoseBodyDriver.
///
/// Run the Python side on the same machine with:
///   python game_bridge.py --webcam --host 127.0.0.1 --port 5005 --show --mirror
/// or, with no camera, to test the whole pipe:
///   python game_bridge.py --sim --host 127.0.0.1 --port 5005
/// </summary>
public class PoseUdpReceiver : MonoBehaviour
{
    [Header("Network")]
    [SerializeField] private int port = 5005;
    [Tooltip("Leave EMPTY when the bridge runs on this machine (127.0.0.1). Only set this when the " +
             "bridge is remote and running with --serve, so we send hello keepalives through NAT.")]
    [SerializeField] private string bridgeHost = "";

    [Header("Gameplay")]
    [Tooltip("Fighter to drive. Auto-found when left empty.")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private bool logEvents = true;

    [Tooltip("Which PlayerMovement attack each pose move triggers (attackName must match exactly).")]
    [SerializeField]
    private MoveAttackBinding[] attackBindings = new MoveAttackBinding[]
    {
        new MoveAttackBinding { move = "punch_left",  attackName = "PunchL" },
        new MoveAttackBinding { move = "punch_right", attackName = "PunchR" },
        new MoveAttackBinding { move = "elbow_left",  attackName = "ElbowL" },
        new MoveAttackBinding { move = "elbow_right", attackName = "ElbowR" },
        new MoveAttackBinding { move = "kick_left",   attackName = "DodgingL" },
        new MoveAttackBinding { move = "kick_right",  attackName = "DodgingR" },
    };

    private UdpClient udp;
    private Thread rxThread;
    private volatile bool running;
    private readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
    private readonly HashSet<int> seenEvents = new HashSet<int>();
    private readonly Dictionary<string, string> moveToAttack = new Dictionary<string, string>();

    private IPEndPoint bridgeEndpoint;
    private float nextHello;
    private static readonly byte[] HelloBytes = Encoding.UTF8.GetBytes("{\"type\":\"hello\"}");

    /// <summary>Most recent pose state (angles, compensation, body). Null until the first packet.</summary>
    public PoseStatePayload LatestState { get; private set; }

    /// <summary>Fired on the main thread for each new move: (move, quality, peakRom).</summary>
    public event Action<string, string, float> OnPoseMove;

    private void Awake()
    {
        if (playerMovement == null)
        {
            playerMovement = FindFirstObjectByType<PlayerMovement>();
        }

        moveToAttack.Clear();
        foreach (MoveAttackBinding binding in attackBindings)
        {
            if (binding != null && !string.IsNullOrEmpty(binding.move))
            {
                moveToAttack[binding.move] = binding.attackName;
            }
        }
    }

    private void Start()
    {
        try
        {
            if (string.IsNullOrEmpty(bridgeHost))
            {
                udp = new UdpClient(port);                    // local bridge: just listen
                Debug.Log($"[PoseUdp] listening on :{port}");
            }
            else
            {
                udp = new UdpClient();                        // remote bridge (--serve): we speak first
                bridgeEndpoint = new IPEndPoint(IPAddress.Parse(bridgeHost), port);
                Debug.Log($"[PoseUdp] hello mode -> {bridgeHost}:{port}");
            }
        }
        catch (SocketException e)
        {
            Debug.LogError($"[PoseUdp] cannot open UDP {port}: {e.Message} " +
                           "(is test_receiver.py or another Unity instance still holding it?)");
            return;
        }

        running = true;
        rxThread = new Thread(ReceiveLoop) { IsBackground = true };
        rxThread.Start();
    }

    private void ReceiveLoop()
    {
        IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = udp.Receive(ref any);           // blocking -> off the main thread
                inbox.Enqueue(Encoding.UTF8.GetString(data));
            }
            catch
            {
                // socket closed during shutdown, or a transient error; loop exits via `running`
            }
        }
    }

    private void Update()
    {
        SendHelloIfDue();

        PoseStatePayload newestState = null;

        // Drain the whole queue each frame. Keep only the newest STATE (older ones are stale and
        // would accumulate lag); process EVERY event, deduped by event_id (the bridge sends 3x).
        while (inbox.TryDequeue(out string json))
        {
            PoseHeader head;
            try { head = JsonUtility.FromJson<PoseHeader>(json); }
            catch { continue; }
            if (head == null) continue;

            if (head.type == "state")
            {
                PoseStateMessage msg = JsonUtility.FromJson<PoseStateMessage>(json);
                if (msg != null) newestState = msg.payload;
            }
            else if (head.type == "event")
            {
                PoseEventMessage msg = JsonUtility.FromJson<PoseEventMessage>(json);
                if (msg?.payload == null) continue;
                if (seenEvents.Add(msg.payload.event_id))
                {
                    HandleMove(msg.payload);
                }
            }
        }

        if (newestState != null)
        {
            LatestState = newestState;
        }
    }

    private void HandleMove(PoseEventPayload e)
    {
        if (logEvents)
        {
            Debug.Log($"[PoseUdp] {e.move} quality={e.quality} rom={e.peak_rom:F0} alpha={e.alpha:F1}");
        }

        OnPoseMove?.Invoke(e.move, e.quality, e.peak_rom);

        if (playerMovement == null)
        {
            Debug.LogWarning("[PoseUdp] no PlayerMovement in scene - move ignored");
            return;
        }

        if (moveToAttack.TryGetValue(e.move, out string attackName))
        {
            playerMovement.TryExecuteAttackByName(attackName);
        }
        else
        {
            Debug.LogWarning($"[PoseUdp] move '{e.move}' has no attack binding");
        }
    }

    // Only used when bridgeHost is set (remote bridge running --serve): keeps the NAT path open.
    private void SendHelloIfDue()
    {
        if (bridgeEndpoint == null || udp == null || Time.unscaledTime < nextHello)
        {
            return;
        }

        nextHello = Time.unscaledTime + 1f;                  // bridge drops us after ~10s of silence
        try { udp.Send(HelloBytes, HelloBytes.Length, bridgeEndpoint); }
        catch (SocketException e) { Debug.LogWarning($"[PoseUdp] hello failed: {e.Message}"); }
    }

    private void OnDestroy()
    {
        running = false;
        udp?.Close();
        udp = null;
    }
}
