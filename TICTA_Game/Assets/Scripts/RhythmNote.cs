using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class RhythmNote : MonoBehaviour
{
    [SerializeField] private RhythmInputGrid inputGrid;
    [SerializeField] private RhythmConductor conductor;
    [SerializeField] private Transform targetSlot;
    [SerializeField] private int slotIndex;
    [SerializeField] private RhythmNoteType noteType = RhythmNoteType.Tap;
    [SerializeField] private float hitTimeSeconds;
    [SerializeField] private float durationSeconds;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Vector3 approachDirection = Vector3.forward;
    [SerializeField] private float hitWindowSeconds = 0.18f;
    [SerializeField] private float missWindowSeconds = 0.35f;

    private float fallbackStartTime;
    private bool holdStarted;
    private bool isFinished;

    private float EndTimeSeconds => hitTimeSeconds + Mathf.Max(0f, durationSeconds);
    private bool UsesConductor => conductor != null;
    private float SongTimeSeconds => UsesConductor ? conductor.SongTimeSeconds : Time.time - fallbackStartTime;

    public int SlotIndex => slotIndex;
    public float HitTimeSeconds => hitTimeSeconds;
    public float DurationSeconds => durationSeconds;
    public RhythmNoteType NoteType => noteType;

    private void Awake()
    {
        if (inputGrid == null)
        {
            inputGrid = FindFirstObjectByType<RhythmInputGrid>();
        }

        Collider noteCollider = GetComponent<Collider>();
        noteCollider.isTrigger = true;

        Rigidbody noteRigidbody = GetComponent<Rigidbody>();
        noteRigidbody.isKinematic = true;
        noteRigidbody.useGravity = false;
    }

    private void OnEnable()
    {
        fallbackStartTime = Time.time;
        if (inputGrid != null)
        {
            inputGrid.OnSlotHeld.AddListener(HandleSlotHeld);
        }
    }

    private void OnDisable()
    {
        if (inputGrid != null)
        {
            inputGrid.OnSlotHeld.RemoveListener(HandleSlotHeld);
        }
    }

    private void Update()
    {
        UpdatePosition();
        UpdateHitState();
    }

    public void Initialize(
        RhythmInputGrid grid,
        RhythmConductor songConductor,
        Transform slotTarget,
        int targetSlotIndex,
        RhythmNoteType targetNoteType,
        float targetHitTimeSeconds,
        float targetDurationSeconds,
        float speed,
        Vector3 movementDirection,
        float hitWindow)
    {
        inputGrid = grid;
        conductor = songConductor;
        targetSlot = slotTarget;
        slotIndex = targetSlotIndex;
        noteType = targetNoteType;
        hitTimeSeconds = targetHitTimeSeconds;
        durationSeconds = Mathf.Max(0f, targetDurationSeconds);
        moveSpeed = Mathf.Max(0.01f, speed);
        approachDirection = movementDirection == Vector3.zero ? Vector3.forward : movementDirection.normalized;
        hitWindowSeconds = Mathf.Max(0.01f, hitWindow);
        fallbackStartTime = Time.time;
        holdStarted = false;
        isFinished = false;
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (targetSlot == null || approachDirection == Vector3.zero)
        {
            return;
        }

        float secondsUntilHit = hitTimeSeconds - SongTimeSeconds;
        transform.position = targetSlot.position - approachDirection.normalized * secondsUntilHit * moveSpeed;
    }

    private void UpdateHitState()
    {
        if (isFinished || inputGrid == null)
        {
            return;
        }

        float songTime = SongTimeSeconds;
        bool slotHeld = inputGrid.IsSlotHeld(slotIndex);

        if (noteType == RhythmNoteType.Tap)
        {
            if (slotHeld && IsWithinWindow(songTime, hitTimeSeconds))
            {
                FinishNote();
                return;
            }

            if (songTime > hitTimeSeconds + missWindowSeconds)
            {
                FinishNote();
            }

            return;
        }

        UpdateCatchState(songTime, slotHeld);
    }

    private void UpdateCatchState(float songTime, bool slotHeld)
    {
        if (!holdStarted)
        {
            if (slotHeld && IsWithinWindow(songTime, hitTimeSeconds))
            {
                FinishNote();
            }
            else if (songTime > hitTimeSeconds + missWindowSeconds)
            {
                FinishNote();
            }

            return;
        }
    }

    private void HandleSlotHeld(int heldSlotIndex)
    {
        if (heldSlotIndex == slotIndex)
        {
            UpdateHitState();
        }
    }

    private bool IsWithinWindow(float songTime, float targetTime)
    {
        return Mathf.Abs(songTime - targetTime) <= hitWindowSeconds;
    }

    private void FinishNote()
    {
        if (isFinished)
        {
            return;
        }

        isFinished = true;
        Destroy(gameObject);
    }

    private void Reset()
    {
        Collider noteCollider = GetComponent<Collider>();
        noteCollider.isTrigger = true;

        Rigidbody noteRigidbody = GetComponent<Rigidbody>();
        noteRigidbody.isKinematic = true;
        noteRigidbody.useGravity = false;
    }

    private void OnValidate()
    {
        slotIndex = Mathf.Clamp(slotIndex, 0, RhythmInputGrid.SlotCount - 1);
        durationSeconds = Mathf.Max(0f, durationSeconds);
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
        hitWindowSeconds = Mathf.Max(0.01f, hitWindowSeconds);
        missWindowSeconds = Mathf.Max(hitWindowSeconds, missWindowSeconds);
    }
}
