using UnityEngine;
using System.Collections;

// กำหนด State ของ AI
public enum EnemyState
{
    Idle,
    Charge,
    Punch,
    Stun
}

public class EnemyBrain : MonoBehaviour
{
    [Header("State Settings")]
    [SerializeField] private EnemyState currentState = EnemyState.Idle;

    [Header("Stun Settings")]
    [SerializeField] private float stunDuration = 1.0f; // ระยะเวลาที่ศัตรูจะติดมึนงง (วินาที)
    [SerializeField] private string stunTriggerName = "Stun"; // ชื่อพารามิเตอร์ใน Animator สำหรับท่าโดนดาเมจ/มึนงง

    [Header("Animator Parameter Names")]
    [SerializeField] private string chargeTriggerName = "Charge"; // ชื่อ Trigger ใน Animator สำหรับท่าชาร์จพลัง
    [SerializeField] private string punchTriggerName = "Punch"; // ชื่อ Trigger ใน Animator สำหรับท่าพุ่งชก
    [SerializeField] private string chargeAnimStateName = "Charge"; // ชื่อ State ของท่าชาร์จใน Animator Controller
    [SerializeField] private string punchAnimStateName = "PunchR"; // ชื่อ State ของท่าต่อยใน Animator Controller

    [Header("Attack & Charge Settings")]
    [SerializeField] private float chargeDuration = 0.8f; // เวลาในการชาร์จยืนนิ่งก่อนพุ่ง (วินาที)
    [SerializeField] private float attackDamage = 10f; // พลังโจมตี
    [SerializeField] private float damageDelay = 0.15f; // ดีเลย์ชกโดนตัว (วินาที)
    [SerializeField] private float attackCooldownDuration = 3f; // ระยะเวลาคูลดาวน์การโจมตี (วินาที)
    [SerializeField] private float detectionRange = 15f; // ระยะตรวจจับผู้เล่น (หน่วย Unity)
    
    [Header("Lunge Settings")]
    [SerializeField] private float stoppingOffset = 0.3f; // ระยะหยุดห่างจากขอบตัวผู้เล่น
    [SerializeField] private float lungeDuration = 0.2f; // ความเร็วพุ่งชก (วินาที)
    [SerializeField] private float returnDuration = 0.2f; // ความเร็วพุ่งกลับ (วินาที)

    private Animator animator;
    private Health health;
    private Rigidbody rb;
    
    // สำหรับล็อคพิกัดยืนของศัตรูไม่ให้เลื่อนไหล
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    // สำหรับเช็คและประมวลผลอินพุตของตัวผู้เล่นเองเพื่อหาขนาด
    private Collider enemyCollider;

    private Coroutine activeBehaviorCoroutine;
    private float nextAttackTime = 0f; // เก็บเวลาที่จะสามารถเริ่มโจมตีรอบถัดไปได้
    private bool wasKinematic; // เก็บค่า isKinematic เดิมก่อนล็อค

    // Property เพื่อให้ภายนอกตรวจสอบ State ปัจจุบันได้
    public EnemyState CurrentState => currentState;

    void Awake()
    {
        // ค้นหา Animator จากตัวเอง หรือจาก child objects (โมเดล 3D มักอยู่เป็นลูก)
        animator = GetComponentInChildren<Animator>();
        health = GetComponent<Health>();
        enemyCollider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();

        if (animator != null)
        {
            Debug.Log($"[EnemyBrain] พบ Animator บน '{animator.gameObject.name}', Controller: {animator.runtimeAnimatorController}");
        }
        else
        {
            Debug.LogError("[EnemyBrain] ไม่พบ Animator component ทั้งบนตัวเองและ child objects! Trigger จะไม่ทำงาน");
        }
    }

    void OnEnable()
    {
        if (health != null)
        {
            // รับอีเวนต์เมื่อโดนดาเมจ เพื่อเปลี่ยนเข้าสู่สถานะ Stun
            health.OnTakeDamage.AddListener(HandleTakeDamage);
        }
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnTakeDamage.RemoveListener(HandleTakeDamage);
        }
    }

    void Start()
    {
        // บันทึกตำแหน่งและองศาการหันตั้งต้นตอนเริ่มเกมไว้
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        // เริ่มต้นให้อยู่ในสถานะ Idle
        TransitionToState(EnemyState.Idle);
    }

    void Update()
    {
        // อัปเดตการทำงาน (Behavior) ในแต่ละเฟรมตามสถานะปัจจุบัน
        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdle();
                break;
            case EnemyState.Charge:
            case EnemyState.Punch:
            case EnemyState.Stun:
                // ในสเตทเหล่านี้จะมีคอรันทีนเฉพาะทางจัดการอยู่ ไม่ต้องทำอะไรเพิ่มใน Update
                break;
        }
    }


    // ฟังก์ชันสำหรับสลับเปลี่ยนสถานะของ AI
    public void TransitionToState(EnemyState newState)
    {
        // ออกจากสถานะเก่า (Exit State)
        ExitState(currentState);

        // เข้าสู่สถานะใหม่
        currentState = newState;
        EnterState(currentState);
    }

    // สิ่งที่ทำเมื่อเริ่มต้นเข้าสู่สถานะใหม่
    private void EnterState(EnemyState state)
    {
        Debug.Log($"[EnemyBrain] {gameObject.name} เข้าสู่สถานะ: {state}");

        switch (state)
        {
            case EnemyState.Idle:
                // ยืนล็อกพิกัดตำแหน่งเริ่มต้น
                transform.position = spawnPosition;
                transform.rotation = spawnRotation;
                break;

            case EnemyState.Charge:
                // เล่นแอนิเมชันเตรียมชาร์จพลัง
                if (animator != null)
                {
                    animator.SetTrigger(chargeTriggerName);
                }
                
                // เริ่มคอรันทีนชาร์จพลังเตรียมโจมตี
                activeBehaviorCoroutine = StartCoroutine(ChargeBehaviorCoroutine());
                break;

            case EnemyState.Punch:
                // ล็อคฟิสิกส์กันถูกผลักขณะพุ่งชก
                FreezePhysics();
                // ค้นหาผู้เล่นเพื่อพุ่งชน
                Transform player = FindPlayer();
                if (player != null)
                {
                    activeBehaviorCoroutine = StartCoroutine(LungeAttackCoroutine(player));
                }
                else
                {
                    Debug.LogWarning("[EnemyBrain] หาตัวผู้เล่นไม่เจอ! ยกเลิกการโจมตี");
                    TransitionToState(EnemyState.Idle);
                }
                break;

            case EnemyState.Stun:
                // เล่นแอนิเมชันมึนงง
                if (animator != null)
                {
                    animator.SetTrigger(stunTriggerName);
                }

                // สั่งให้นับเวลาถอยหลังเพื่อหลุดพ้นจากสถานะ Stun
                activeBehaviorCoroutine = StartCoroutine(StunTimerCoroutine());
                break;
        }
    }

    // พฤติกรรมที่ประมวลผลต่อเนื่องใน Update
    private void UpdateIdle()
    {
        // ยืนล็อคพิกัดตำแหน่งเริ่มต้น
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;

        // ถ้ายังไม่หมดคูลดาวน์ -> รอต่อ
        if (Time.time < nextAttackTime) return;

        // หาผู้เล่นจาก Tag "Player"
        Transform player = FindPlayer();
        if (player == null) return;

        Health playerHealth = player.GetComponent<Health>();
        if (playerHealth != null && !playerHealth.IsDead)
        {
            // เจอผู้เล่นและยังมีชีวิตอยู่ -> เริ่มชาร์จเตรียมโจมตี
            TransitionToState(EnemyState.Charge);
        }
    }

    // สิ่งที่ทำก่อนจะย้ายออกจากสถานะเดิม
    private void ExitState(EnemyState state)
    {
        // ยุติการทำงานของพฤติกรรมในสถานะเก่าทันที เพื่อให้สถานะใหม่ขึ้นมาทำงานได้อย่างสมบูรณ์
        if (activeBehaviorCoroutine != null)
        {
            StopCoroutine(activeBehaviorCoroutine);
            activeBehaviorCoroutine = null;
        }

        switch (state)
        {
            case EnemyState.Idle:
                break;
            case EnemyState.Charge:
                UnfreezePhysics();
                break;
            case EnemyState.Punch:
                UnfreezePhysics();
                break;
            case EnemyState.Stun:
                // รีเซ็ต Trigger ในกรณีติด Stun แล้วฟื้น เพื่อไม่ให้ Trigger ค้างไปยิงซ้ำ
                if (animator != null)
                {
                    animator.ResetTrigger(stunTriggerName);
                }
                break;
        }
    }

    // เมื่อได้รับความเสียหายจาก Health.cs
    private void HandleTakeDamage(float damageAmount)
    {
        // หากศัตรูกำลังชาร์จ (Charge) หรือกำลังพุ่งโจมตี (Punch) จะมีสถานะป้องกันการโดนขัดจังหวะ (ขัดไม่ได้)
        if (currentState == EnemyState.Charge || currentState == EnemyState.Punch)
        {
            Debug.Log($"[EnemyBrain] {gameObject.name} โดนดาเมจแต่ไม่ติด Stun เนื่องจากอยู่ในสถานะ: {currentState}");
            return;
        }

        // ถ้าอยู่นอกเหนือนั้น (เช่น Idle) โดนดาเมจแล้วจะเปลี่ยนเป็นสถานะ Stun ทันที
        TransitionToState(EnemyState.Stun);
    }

    // ล็อค Rigidbody กันถูกผลัก (เรียกตอนเริ่ม Charge/Punch)
    private void FreezePhysics()
    {
        if (rb != null)
        {
            wasKinematic = rb.isKinematic;
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // ปลดล็อค Rigidbody กลับสู่ปกติ
    private void UnfreezePhysics()
    {
        if (rb != null)
        {
            rb.isKinematic = wasKinematic;
        }
    }

    // คอร์รูทีนชาร์จพลัง — ล็อคตำแหน่งทุกเฟรมตลอดช่วงชาร์จ
    private IEnumerator ChargeBehaviorCoroutine()
    {
        FreezePhysics();

        Debug.Log($"[EnemyBrain] เริ่มรัน ChargeBehaviorCoroutine. chargeAnimStateName='{chargeAnimStateName}', chargeDuration={chargeDuration}");

        // รอ 1 เฟรมให้ trigger ใน Animator ทำงาน
        yield return null;

        if (animator != null && !string.IsNullOrEmpty(chargeAnimStateName))
        {
            // รอจนกระทั่ง Animator เริ่มทำการทรานซิชัน หรือเข้าสู่ state การชาร์จโดยตรง
            float elapsed = 0f;
            while (!animator.GetCurrentAnimatorStateInfo(0).IsName(chargeAnimStateName) && 
                   !animator.GetNextAnimatorStateInfo(0).IsName(chargeAnimStateName))
            {
                transform.position = spawnPosition;
                transform.rotation = spawnRotation;
                elapsed += Time.deltaTime;
                if (elapsed > 1.0f) // ตัวช่วยหลุดกรณีฉุกเฉิน (1 วินาที)
                {
                    var curState = animator.GetCurrentAnimatorStateInfo(0);
                    Debug.LogWarning($"[EnemyBrain] รอเริ่มทรานซิชันเข้าสู่ Charge นานเกิน 1 วินาที! CurrentStateHash={curState.shortNameHash}");
                    break;
                }
                yield return null;
            }

            // ตอนนี้ Animator เริ่มเปลี่ยนสเตทเข้าสู่ Charge แล้ว
            // รอจนกระทั่งอนิเมชันชาร์จเล่นไปจนถึงอย่างน้อย 99% ของเวลาในสเตท
            elapsed = 0f;
            while (true)
            {
                transform.position = spawnPosition;
                transform.rotation = spawnRotation;

                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                var nextStateInfo = animator.GetNextAnimatorStateInfo(0);

                float progress = 0f;
                bool inChargeState = false;

                if (stateInfo.IsName(chargeAnimStateName))
                {
                    progress = stateInfo.normalizedTime;
                    inChargeState = true;
                }
                else if (nextStateInfo.IsName(chargeAnimStateName))
                {
                    progress = nextStateInfo.normalizedTime;
                    inChargeState = true;
                }

                // ถ้าไม่ได้อยู่ในสเตท Charge หรือความคืบหน้าของเวลารันอนิเมชันครบ 99% แล้ว ให้จบการรอ
                if (!inChargeState || (progress % 1.0f) >= 0.99f)
                {
                    Debug.Log($"[EnemyBrain] ออกจากลูปการรอ Charge: inChargeState={inChargeState}, progress={progress:F2}");
                    break;
                }

                elapsed += Time.deltaTime;
                if (elapsed > 3.0f) // ตัวช่วยหลุดกรณีฉุกเฉิน (3 วินาที)
                {
                    Debug.LogWarning("[EnemyBrain] รอเล่นอนิเมชัน Charge ครบรอบนานเกิน 3 วินาที!");
                    break;
                }
                yield return null;
            }
        }
        else
        {
            Debug.Log("[EnemyBrain] ไม่ได้ระบุ Animator หรือ chargeAnimStateName ว่างเปล่า -> ใช้ระบบชาร์จแบบเวลาคงที่ (Fallback)");
            // Fallback ใช้เวลาดีเลย์แบบคงที่
            float elapsed = 0f;
            while (elapsed < chargeDuration)
            {
                // ล็อคตำแหน่งทุกเฟรม กันถูกผลักขยับ
                transform.position = spawnPosition;
                transform.rotation = spawnRotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        // ชาร์จเสร็จ เปลี่ยนไปลุยสเตทออกหมัด (Punch) ทันที
        activeBehaviorCoroutine = null;
        TransitionToState(EnemyState.Punch);
    }

    // ฟังก์ชันช่วยหาจุดที่ใกล้ที่สุดบน Collider อย่างปลอดภัย (รองรับ CharacterController และป้องกันการโยน Exception)
    private Vector3 GetClosestPoint(Collider col, Vector3 toPosition)
    {
        if (col == null) return toPosition;

        // หากเป็น CharacterController (ซึ่งมักไม่มีการรองรับ ClosestPoint ดั้งเดิมที่ดีพอในบางเวอร์ชัน)
        if (col is CharacterController cc)
        {
            Vector3 center = cc.transform.TransformPoint(cc.center);
            float halfHeight = Mathf.Max(0f, (cc.height * 0.5f) - cc.radius);
            Vector3 top = center + Vector3.up * halfHeight;
            Vector3 bottom = center + Vector3.down * halfHeight;
            
            Vector3 dir = top - bottom;
            float length = dir.magnitude;
            if (length < 0.0001f) return center;
            
            dir.Normalize();
            float t = Vector3.Dot(toPosition - bottom, dir);
            t = Mathf.Clamp(t, 0f, length);
            Vector3 closestPointOnSegment = bottom + dir * t;
            
            Vector3 toClosest = toPosition - closestPointOnSegment;
            float dist = toClosest.magnitude;
            if (dist > 0.0001f)
            {
                return closestPointOnSegment + (toClosest / dist) * (cc.radius * Mathf.Max(cc.transform.localScale.x, cc.transform.localScale.z));
            }
            return closestPointOnSegment;
        }

        // สำหรับ Collider ทั่วไป
        try
        {
            return col.ClosestPoint(toPosition);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[EnemyBrain] ClosestPoint ล้มเหลวสำหรับ {col.name}: {e.Message} (จะสลับไปใช้ Bounds แทน)");
            return col.bounds.ClosestPoint(toPosition);
        }
    }

    // คอรันทีนการพุ่งโจมตีผู้เล่น (โลจิกเหมือนกับ Player)
    private IEnumerator LungeAttackCoroutine(Transform target)
    {
        // คำนวณทิศทางและระยะหยุด
        Collider playerCollider = target.GetComponent<Collider>();
        CharacterController playerCC = target.GetComponent<CharacterController>();
        // คำนวณทิศทางและระยะหยุด (แบนบนแกน XZ เพื่อป้องกันความต่างระดับความสูงแกน Y)
        Vector3 targetPos = target.position;
        targetPos.y = spawnPosition.y;
        Vector3 direction = (targetPos - spawnPosition).normalized;
        Vector3 stoppingPosition;

        // คำนวณขนาดความกว้าง (รัศมี) ของตัวศัตรูเอง
        float selfSizeOffset = 0f;
        if (enemyCollider != null)
        {
            if (enemyCollider is CapsuleCollider capsule)
                selfSizeOffset = capsule.radius * Mathf.Max(transform.localScale.x, transform.localScale.z);
            else if (enemyCollider is SphereCollider sphere)
                selfSizeOffset = sphere.radius * Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
            else if (enemyCollider is BoxCollider box)
            {
                Vector3 localDir = transform.InverseTransformDirection(direction);
                Vector3 halfSize = box.size * 0.5f;
                selfSizeOffset = Mathf.Abs(localDir.x * halfSize.x * transform.localScale.x) +
                                 Mathf.Abs(localDir.z * halfSize.z * transform.localScale.z);
            }
        }

        // คำนวณขนาดความกว้าง (รัศมี) ของผู้เล่น
        float playerSizeOffset = 0f;
        if (playerCollider != null)
        {
            if (playerCollider is CapsuleCollider playerCapsule)
                playerSizeOffset = playerCapsule.radius * Mathf.Max(target.localScale.x, target.localScale.z);
            else if (playerCollider is SphereCollider playerSphere)
                playerSizeOffset = playerSphere.radius * Mathf.Max(target.localScale.x, target.localScale.y, target.localScale.z);
            else if (playerCollider is BoxCollider playerBox)
            {
                Vector3 localDir = target.InverseTransformDirection(-direction);
                Vector3 halfSize = playerBox.size * 0.5f;
                playerSizeOffset = Mathf.Abs(localDir.x * halfSize.x * target.localScale.x) +
                                   Mathf.Abs(localDir.z * halfSize.z * target.localScale.z);
            }
        }
        else if (playerCC != null)
        {
            playerSizeOffset = playerCC.radius * Mathf.Max(target.localScale.x, target.localScale.z);
        }

        if (playerCollider != null)
        {
            // หาขอบตัวผู้เล่นที่ใกล้ศัตรูที่สุดแบบปลอดภัย
            Vector3 closestPoint = GetClosestPoint(playerCollider, spawnPosition);
            closestPoint.y = spawnPosition.y;

            // ในเมื่อได้จุดผิวของผู้เล่น (closestPoint) แล้ว ระยะหยุดถอยห่างออกมาจะเท่ากับ (ขนาดตัวศัตรู + offset)
            float totalBuffer = selfSizeOffset + stoppingOffset;
            stoppingPosition = closestPoint - (direction * totalBuffer);
        }
        else
        {
            // Fallback ในกรณีไม่มี Collider ใช้จุดศูนย์กลางผู้เล่น แล้วลบขนาดรัศมีผู้เล่นรวมไปด้วย
            float totalBuffer = selfSizeOffset + playerSizeOffset + stoppingOffset;
            stoppingPosition = targetPos - (direction * totalBuffer);
        }
        stoppingPosition.y = spawnPosition.y;

        // เช็คกันพุ่งเลยหัว (ถ้าจุดหยุดอยู่หลังตัวศัตรู = ใกล้เกินไปแล้ว)
        if (Vector3.Dot(stoppingPosition - spawnPosition, direction) <= 0)
        {
            stoppingPosition = spawnPosition;
        }

        // หันหน้ามองผู้เล่นแล้วเริ่มสไลด์พุ่งไป
        if (direction != Vector3.zero && stoppingPosition != spawnPosition)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // 1. พุ่งไปหาผู้เล่น (Lunge)
        float elapsed = 0f;
        while (elapsed < lungeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lungeDuration;
            Vector3 currentPos = Vector3.Lerp(spawnPosition, stoppingPosition, t);
            currentPos.y = spawnPosition.y;
            transform.position = currentPos;
            yield return null;
        }
        transform.position = stoppingPosition;

        yield return null; // รอ 1 เฟรมให้ภาพ Render สนิท

        // 2. ปล่อยแอนิเมชั่นต่อย
        if (animator != null)
        {
            animator.SetTrigger(punchTriggerName);
            Debug.Log($"[EnemyBrain] ตั้งค่า Punch trigger: {punchTriggerName}");
        }

        if (animator != null && !string.IsNullOrEmpty(punchAnimStateName))
        {
            // รอจนกระทั่ง Animator เริ่มทำการทรานซิชัน หรือเข้าสู่ state การต่อยโดยตรง
            float punchElapsed = 0f;
            while (!animator.GetCurrentAnimatorStateInfo(0).IsName(punchAnimStateName) && 
                   !animator.GetNextAnimatorStateInfo(0).IsName(punchAnimStateName))
            {
                punchElapsed += Time.deltaTime;
                if (punchElapsed > 1.0f) // ตัวช่วยหลุดกรณีฉุกเฉิน (1 วินาที)
                {
                    Debug.LogWarning("[EnemyBrain] รอเริ่มทรานซิชันเข้าสู่ PunchR นานเกิน 1 วินาที!");
                    break;
                }
                yield return null;
            }

            // เมื่อเริ่มทรานซิชันแล้ว ค่อยรีเซ็ต trigger เพื่อป้องกันปัญหากลืนทริกเกอร์เร็วเกินไป
            animator.ResetTrigger(punchTriggerName);
            Debug.Log($"[EnemyBrain] รีเซ็ต Punch trigger: {punchTriggerName}");

            // รอตามเวลาดีเลย์เพื่อให้หมัดเอื้อมไปถึงตัวผู้เล่น แล้วค่อยทำความเสียหาย
            yield return new WaitForSeconds(damageDelay);

            Health playerHealth = target.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
            }

            // รอจนกว่าแอนิเมชันชกจะเล่นจบอย่างสมบูรณ์ (หรือความคืบหน้าครบ 99%)
            punchElapsed = 0f;
            while (true)
            {
                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                var nextStateInfo = animator.GetNextAnimatorStateInfo(0);

                float progress = 0f;
                bool inPunchState = false;

                if (stateInfo.IsName(punchAnimStateName))
                {
                    progress = stateInfo.normalizedTime;
                    inPunchState = true;
                }
                else if (nextStateInfo.IsName(punchAnimStateName))
                {
                    progress = nextStateInfo.normalizedTime;
                    inPunchState = true;
                }

                // ถ้าไม่ได้อยู่ในสเตท Punch หรือความคืบหน้าของเวลารันอนิเมชันครบ 99% แล้ว ให้จบการรอ
                if (!inPunchState || (progress % 1.0f) >= 0.99f)
                {
                    break;
                }

                punchElapsed += Time.deltaTime;
                if (punchElapsed > 3.0f) // ตัวช่วยหลุดกรณีฉุกเฉิน (3 วินาที)
                {
                    Debug.LogWarning("[EnemyBrain] รอเล่นอนิเมชัน PunchR ครบรอบนานเกิน 3 วินาที!");
                    break;
                }
                yield return null;
            }
        }
        else
        {
            // Fallback กรณีไม่มีชื่ออนิเมชันให้ตรวจจับ - ใช้เวลาดีเลย์แบบคงที่
            yield return null;
            if (animator != null)
            {
                animator.ResetTrigger(punchTriggerName);
            }

            yield return new WaitForSeconds(damageDelay);

            Health playerHealth = target.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
            }

            // รอจังหวะท่าชกค้างสักพักก่อนถอยกลับ
            yield return new WaitForSeconds(0.2f);
        }

        // 3. สไลด์ถอยกลับจุดเดิม
        elapsed = 0f;
        Vector3 reachedPosition = transform.position;
        reachedPosition.y = spawnPosition.y;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnDuration;
            Vector3 currentPos = Vector3.Lerp(reachedPosition, spawnPosition, t);
            currentPos.y = spawnPosition.y;
            transform.position = currentPos;
            yield return null;
        }

        // เซ็ตเวลาคูลดาวน์ก่อนจะโจมตีรอบใหม่ได้
        nextAttackTime = Time.time + attackCooldownDuration;

        // กลับไปตั้งหลักที่ Idle
        activeBehaviorCoroutine = null;
        TransitionToState(EnemyState.Idle);
    }

    // คอร์รูทีนสำหรับนับเวลาถอยหลังการหายมึนงง (Stun)
    private IEnumerator StunTimerCoroutine()
    {
        yield return new WaitForSeconds(stunDuration);

        // เมื่อฟื้นตัว ถ้ายังไม่ตาย ให้กลับไปเริ่มลูปที่ Idle
        if (health != null && !health.IsDead)
        {
            // ตั้งเวลาให้ศัตรูยังไม่โจมตีทันทีหลังหายมึน (คูลดาวน์ใหม่ชั่วคราว)
            nextAttackTime = Time.time + 1.0f; 
            activeBehaviorCoroutine = null;
            TransitionToState(EnemyState.Idle);
        }
    }

    // ฟังก์ชันค้นหาผู้เล่นที่มี Tag ว่า "Player"
    private Transform FindPlayer()
    {
        GameObject player = GameObject.FindWithTag("Player");
        return player != null ? player.transform : null;
    }
}

