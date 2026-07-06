using UnityEngine;
using UnityEngine.InputSystem; // นำเข้า New Input System
using System.Collections; // สำหรับใช้งาน Coroutine

public enum PlayerState
{
    Idle,
    Lunge,
    Punch,
    Return,
    Dodge,
    Stun
}

// โครงสร้างข้อมูลสำหรับท่าโจมตีหรือการกระทำแต่ละท่า
[System.Serializable]
public struct PlayerAttack
{
    public string attackName; // ชื่อท่าอ้างอิง เช่น Punch, Kick, Heavy Punch

    [Header("Input Bindings")]
    public Key keyboardKey; // ปุ่มกดบนคีย์บอร์ด (เช่น Digit1, Digit2, Space)
    public bool useMouseLeftClick; // ถ้าตั้งเป็น true จะกดใช้ด้วยการคลิกซ้าย แทนที่จะเป็นปุ่มคีย์บอร์ด

    [Header("Animation Settings")]
    public string triggerName; // ชื่อ Trigger ใน Animator
    public string animStateName; // ชื่อ State ของท่านี้ใน Animator Controller

    [Header("Combat Settings")]
    public Cooldown cooldown; // คลาสสำหรับเก็บระบบคูลดาวน์แยกกันของแต่ละท่า
    public float damage; // พลังโจมตี
    public float damageDelay; // ดีเลย์ก่อนพลังชีวิตศัตรูลดลง (ให้ตรงกับจังหวะหมัด/เท้าโดนตัว)

    [Header("Dodge Settings")]
    public bool isDodge; // ระบุว่าเป็นท่าหลบหรือไม่ (จะได้รับ I-Frame อมตะระหว่างทำท่านี้)
    public bool dodgeBackward; // หาก true จะหลบไปข้างหลัง, false = forward (เพิ่ม forward lunge)
    public bool dodgeLeft; // จะหลบไปด้านซ้าย
    public bool dodgeRight; // จะหลบไปด้านขวา
    public float dodgeDistance; // ระยะการเคลื่อนที่ไปข้างหน้า/หลัง (หน่วย Unity)
    public float dodgeLateralDistance; // ระยะการเคลื่อนที่ด้านข้าง (หน่วย Unity)
    public float dodgeForwardLunge; // ระยะการพุ่งไปข้างหน้าเพิ่มเติม (0 = ไม่มี)
    public float dodgeDuration; // เวลาที่ใช้สไลด์หลบ (วินาที)
}

public class PlayerMovement : MonoBehaviour
{
    private Animator animator;
    
    [Header("State Settings")]
    [SerializeField] private PlayerState currentState = PlayerState.Idle;

    [Header("Stun Settings")]
    [SerializeField][Min(0f)] private float stunDuration = 1.0f;
    [SerializeField] private string stunTriggerName = "Stun";
    
    // ตัวแปรล็อกพิกัดเริ่มต้น เพื่อแก้ไขปัญหาตัวละครตำแหน่งไม่นิ่ง หรือเลื่อนไหล (Drift) เองจากอนิเมชั่น
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    // ตัวแปรเก็บ Collider ของตัวผู้เล่นเอง
    private Collider playerCollider;
    private CharacterController playerCC;

    [Header("Lunge & Return Settings")]
    [SerializeField] private float stoppingOffset = 0.3f; // ระยะห่างจากขอบ Collider ของศัตรูที่จะไปหยุดยืน
    [SerializeField] private float lungeDuration = 0.15f; // เวลาพุ่งขาไป (วินาที)
    [SerializeField] private float returnDuration = 0.15f; // เวลาพุ่งกลับมาที่เดิม (วินาที)
    [SerializeField] private float dodgeReturnDuration = 0.4f; // เวลาเดินทางกลับหลังทำท่าหลบ (ปรับแยกต่างหากจาก returnDuration ปกติ)

    [Header("Attacks Settings")]
    [SerializeField] private PlayerAttack[] attacks; // รายการท่าโจมตีทั้งหมด (เพิ่มลดจำนวนท่าได้อิสระจาก Inspector)

    [Header("Camera Feedback")]
    [SerializeField] private CameraShakeProfile damageShakeProfile = new CameraShakeProfile();
    [SerializeField] private bool logDamageShakeEvents = true;
    [SerializeField] private CameraFovProfile dodgeFovProfile = new CameraFovProfile(); // FOV zoom-out ระหว่าง Dodge
    [SerializeField] private CameraFovProfile punchFovProfile = new CameraFovProfile();  // FOV ตอนออกท่าโจมตี

    private Health health;
    private IFrameController iframeController;
    private Coroutine stunCoroutine;

    // Property เพื่อให้ภายนอกเช็คสถานะของผู้เล่นได้
    public PlayerState CurrentState => currentState;

    void Awake()
    {
        health = GetComponent<Health>();
        iframeController = FindFirstObjectByType<IFrameController>();
    }

    void OnEnable()
    {
        if (health != null)
        {
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
        animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError("ไม่พบ Animator Component!");
        }

        // บันทึกตำแหน่งและองศาการหันตั้งต้นตอนเริ่มเกมไว้ เพื่อใช้ล็อคพิกัดให้มั่นคง 100%
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        // ดึงคอมโพเนนต์ Collider หรือ CharacterController ของผู้เล่นเองมาเก็บไว้
        playerCollider = GetComponent<Collider>();
        playerCC = GetComponent<CharacterController>();

        // เริ่มต้นด้วย Idle
        TransitionToState(PlayerState.Idle);
    }

    void Update()
    {
        if (animator == null) return;

        // แยกโค้ดทำงานและเงื่อนไขตาม State ปัจจุบัน
        switch (currentState)
        {
            case PlayerState.Idle:
                UpdateIdleState();
                break;
            case PlayerState.Lunge:
            case PlayerState.Punch:
            case PlayerState.Return:
            case PlayerState.Dodge:
                // ในสเตทเคลื่อนไหวเหล่านี้ จะมี Coroutine รับหน้าที่จัดการอยู่ ไม่ต้องทำอะไรใน Update
                break;
            case PlayerState.Stun:
                UpdateStunState();
                break;
        }
    }

    // ฟังก์ชันหลักในการสลับสเตทของผู้เล่น
    public void TransitionToState(PlayerState newState)
    {
        ExitState(currentState);
        currentState = newState;
        EnterState(currentState);
    }

    private void EnterState(PlayerState state)
    {
        Debug.Log($"[PlayerMovement] เข้าสู่สถานะ: {state}");
        switch (state)
        {
            case PlayerState.Idle:
                // ล็อคพิกัดตำแหน่งตัวผู้เล่นให้อยู่จุดเริ่มต้นเสมอ ป้องกันการขยับสไลด์หลุดจากแอนิเมชัน
                transform.position = spawnPosition;
                transform.rotation = spawnRotation;
                break;
            case PlayerState.Lunge:
                break;
            case PlayerState.Punch:
                break;
            case PlayerState.Return:
                break;
            case PlayerState.Dodge:
                break;
            case PlayerState.Stun:
                transform.position = spawnPosition;
                transform.rotation = spawnRotation;
                ResetAttackTriggers();
                if (animator != null && !string.IsNullOrEmpty(stunTriggerName))
                {
                    animator.SetTrigger(stunTriggerName);
                }
                stunCoroutine = StartCoroutine(StunTimerCoroutine());
                break;
        }
    }

    private void ExitState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Idle:
                break;
            case PlayerState.Lunge:
                break;
            case PlayerState.Punch:
                break;
            case PlayerState.Return:
                break;
            case PlayerState.Dodge:
                break;
            case PlayerState.Stun:
                if (stunCoroutine != null)
                {
                    StopCoroutine(stunCoroutine);
                    stunCoroutine = null;
                }
                if (animator != null && !string.IsNullOrEmpty(stunTriggerName))
                {
                    animator.ResetTrigger(stunTriggerName);
                }
                break;
        }
    }

    // ทำงานเฉพาะเมื่ออยู่ในสถานะ Idle เท่านั้น
    private void UpdateIdleState()
    {
        // ยืนล็อคตำแหน่งอยู่กับที่
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;

        if (attacks == null || attacks.Length == 0) return;

        // วนลูปเช็คอินพุตของทุกท่าในอาร์เรย์
        for (int i = 0; i < attacks.Length; i++)
        {
            if (CheckAttackInput(attacks[i]))
            {
                ExecuteAttack(attacks[i]);
                break; // กดออกท่าได้ทีละ 1 ท่าใน 1 เฟรม
            }
        }
    }

    private void UpdateStunState()
    {
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;
    }

    // ฟังก์ชันช่วยดักจับว่ามีการกดอินพุตของท่าโจมตีนั้นๆ หรือไม่
    private bool CheckAttackInput(PlayerAttack attack)
    {
        if (attack.useMouseLeftClick)
        {
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }
        else
        {
            return Keyboard.current != null && Keyboard.current[attack.keyboardKey].wasPressedThisFrame;
        }
    }

    // ฟังก์ชันจัดการออกท่าโจมตี
    private void ExecuteAttack(PlayerAttack attack)
    {
        if (attack.cooldown.IsReady)
        {
            Transform targetEnemy = FindEnemy();

            if (attack.isDodge)
            {
                Debug.Log($"[Debug] ออกท่าหลบ: {attack.attackName}");
                StartCoroutine(DodgeCoroutine(targetEnemy, attack));
            }
            else
            {
                // ค้นหาศัตรูตัวเดียวในฉาก
                if (targetEnemy != null)
                {
                    float dist = Vector3.Distance(spawnPosition, targetEnemy.position);
                    Debug.Log($"[Debug] พบศัตรู: {targetEnemy.name} | ระยะห่างจริง: {dist:F2} | ออกท่า: {attack.attackName}");
                    
                    // เริ่มคอรันทีนการพุ่งโจมตีโดยส่งโครงสร้างข้อมูลท่านี้เข้าไปประมวลผล
                    StartCoroutine(LungeAndReturnCoroutine(targetEnemy, attack));
                }
                else
                {
                    // หากไม่พบศัตรู ให้ปล่อยท่าโจมตีอยู่กับที่ตามปกติ
                    TransitionToState(PlayerState.Punch);
                    animator.SetTrigger(attack.triggerName);
                    attack.cooldown.StartCooldown();
                    
                    StartCoroutine(StationaryPunchCoroutine(attack.triggerName, attack.animStateName));
                    Debug.LogError($"[Debug] ไม่พบศัตรูในฉาก! (ปล่อยท่า {attack.attackName} อยู่กับที่)");
                }
            }
        }
        else
        {
            Debug.Log($"ท่า {attack.attackName} ติดคูลดาวน์เหลืออีก: {attack.cooldown.TimeRemaining:F2} วินาที");
        }
    }

    /// <summary>
    /// พุ่งไปหาศัตรู -> เล่นอนิเมชั่นท่าโจมตี -> สไลด์ถอยหลังกลับมาที่เดิม
    /// </summary>
    private IEnumerator LungeAndReturnCoroutine(Transform target, PlayerAttack attack)
    {
        // 1. เริ่มพุ่ง (Lunge)
        TransitionToState(PlayerState.Lunge);

        // หาจุดขอบ Collider ของศัตรูที่ใกล้ผู้เล่นที่สุด
        Collider enemyCollider = target.GetComponent<Collider>();
        Vector3 direction = (target.position - spawnPosition).normalized;
        Vector3 stoppingPosition;

        // คำนวณขนาดความกว้าง (รัศมี) ของตัวผู้เล่นเอง
        float playerSizeOffset = 0f;
        if (playerCollider != null)
        {
            if (playerCollider is CapsuleCollider capsule)
            {
                playerSizeOffset = capsule.radius * Mathf.Max(transform.localScale.x, transform.localScale.z);
            }
            else if (playerCollider is SphereCollider sphere)
            {
                playerSizeOffset = sphere.radius * Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
            }
            else if (playerCollider is BoxCollider box)
            {
                // หาระยะขอบกล่องตามทิศทางที่พุ่งไป
                Vector3 localDir = transform.InverseTransformDirection(direction);
                Vector3 halfSize = box.size * 0.5f;
                playerSizeOffset = Mathf.Abs(localDir.x * halfSize.x * transform.localScale.x) + 
                                   Mathf.Abs(localDir.y * halfSize.y * transform.localScale.y) + 
                                   Mathf.Abs(localDir.z * halfSize.z * transform.localScale.z);
            }
            else
            {
                // สำหรับ Collider ทั่วไป
                Vector3 testPoint = transform.position + direction * 10f;
                Vector3 closestPointOnSelf = playerCollider.ClosestPoint(testPoint);
                playerSizeOffset = Vector3.Distance(transform.position, closestPointOnSelf);
            }
        }
        else if (playerCC != null)
        {
            playerSizeOffset = playerCC.radius * Mathf.Max(transform.localScale.x, transform.localScale.z);
        }

        // รวมระยะหยุดห่างจากขอบ = รัศมีผู้เล่น + ระยะ Offset ที่ตั้งไว้
        float totalBuffer = playerSizeOffset + stoppingOffset;

        if (enemyCollider != null)
        {
            // ClosestPoint จะคืนจุดบนผิว Collider ที่ใกล้ผู้เล่นที่สุด
            Vector3 closestPoint = enemyCollider.ClosestPoint(spawnPosition);
            closestPoint.y = spawnPosition.y; // ล็อคแกน Y

            // หยุดห่างจากขอบ Collider ของศัตรู เท่ากับขนาดตัวผู้เล่น + offset
            stoppingPosition = closestPoint - (direction * totalBuffer);

            float distToStop = Vector3.Distance(spawnPosition, stoppingPosition);
            Debug.Log($"[Debug] ขอบศัตรู: {closestPoint} | รัศมีผู้เล่น: {playerSizeOffset:F2} | หยุดที่: {stoppingPosition} | ระยะพุ่ง: {distToStop:F2}");

            // ถ้าจุดหยุดอยู่หลังผู้เล่น (ใกล้เกินไปแล้ว) ให้ยืนอยู่กับที่
            if (Vector3.Dot(stoppingPosition - spawnPosition, direction) <= 0)
            {
                stoppingPosition = spawnPosition;
                Debug.Log("[Debug] ผู้เล่นอยู่ใกล้ศัตรูมากพอแล้ว -> ชกอยู่กับที่");
            }
        }
        else
        {
            // ถ้าไม่มี Collider ศัตรู ให้พุ่งไปหาตำแหน่งศัตรูแล้วหยุดห่างตาม totalBuffer
            Vector3 targetPos = target.position;
            targetPos.y = spawnPosition.y;
            stoppingPosition = targetPos - (direction * totalBuffer);
            Debug.LogWarning("[Debug] ศัตรูไม่มี Collider! ใช้ตำแหน่ง transform แทน");
        }
        stoppingPosition.y = spawnPosition.y; // ล็อคระดับความสูงแกน Y

        // หมุนตัวและพุ่งไปหาศัตรู (Lunge Move)
        if (direction != Vector3.zero && stoppingPosition != spawnPosition)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        float elapsed = 0f;
        while (elapsed < lungeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lungeDuration;
            Vector3 currentPos = Vector3.Lerp(spawnPosition, stoppingPosition, t);
            currentPos.y = spawnPosition.y; // ป้องกันบิดเบี้ยวแกน Y
            transform.position = currentPos;
            yield return null;
        }
        transform.position = stoppingPosition;

        // รอ 1 เฟรมเพื่อให้ Unity อัพเดตตำแหน่งบนหน้าจอก่อนจะเริ่มทำท่า
        yield return null;

        // 2. ทรานซิชันเข้าสู่สเตทโจมตี (Punch)
        TransitionToState(PlayerState.Punch);
        CameraFovEffect.PlayGlobal(punchFovProfile); // เล่น FOV effect ตอนออกหมัด
        animator.SetTrigger(attack.triggerName);
        attack.cooldown.StartCooldown();

        // รอ 1 เฟรมให้ trigger ถูก consume แล้วรีเซ็ตทิ้ง ป้องกัน trigger ค้างทำให้โจมตีซ้ำ
        yield return null;
        animator.ResetTrigger(attack.triggerName);

        // รอให้ Animator เข้าสู่ state การทำงานของท่านั้นๆ ก่อน
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(attack.animStateName))
        {
            yield return null;
        }

        // รอตามเวลาดีเลย์เพื่อให้มือ/เท้าเอื้อมไปถึงตัวศัตรู แล้วค่อยทำความเสียหาย
        yield return new WaitForSeconds(attack.damageDelay);

        Health enemyHealth = target.GetComponent<Health>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(attack.damage);
            PlayDamageCameraShake(attack.damage, $"hit {target.name} with {attack.attackName}");
        }
        else
        {
            Debug.LogWarning($"[Debug] ไม่พบสคริปต์ Health บน {target.name} จึงสร้างความเสียหายไม่ได้!");
        }

        // รอเวลาที่เหลือของอนิเมชั่นสกิลจนกว่าจะเล่นจบแล้วเปลี่ยนไปสเตท Return
        while (animator.GetCurrentAnimatorStateInfo(0).IsName(attack.animStateName))
        {
            yield return null;
        }

        // 3. ทรานซิชันเข้าสู่สเตทเดินทางกลับ (Return)
        TransitionToState(PlayerState.Return);
        elapsed = 0f;
        Vector3 reachedPosition = transform.position;
        reachedPosition.y = spawnPosition.y;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnDuration;
            Vector3 currentPos = Vector3.Lerp(reachedPosition, spawnPosition, t);
            currentPos.y = spawnPosition.y; // ล็อคแกน Y
            transform.position = currentPos;
            yield return null;
        }

        // 4. สิ้นสุดการทำงาน กลับสู่สเตท Idle
        TransitionToState(PlayerState.Idle);
    }

    /// <summary>
    /// คอรันทีนควบคุมท่าหลบ: สไลด์ถอยหลังพร้อมเปิด I-Frame อมตะ -> เล่นอนิเมชันหลบ -> สไลด์กลับมาที่เดิม
    /// </summary>
    private IEnumerator DodgeCoroutine(Transform target, PlayerAttack attack)
    {
        // 1. เข้าสู่สถานะ Dodge
        TransitionToState(PlayerState.Dodge);

        // เปิดใช้งาน I-Frame (อมตะ)
        if (iframeController != null)
        {
            iframeController.SetInvincible(true);
        }
        else if (health != null)
        {
            health.IsInvincible = true;
        }

        // เล่น FOV zoom-out ตอนเริ่ม Dodge
        CameraFovEffect.PlayGlobal(dodgeFovProfile);

        animator.SetTrigger(attack.triggerName);
        attack.cooldown.StartCooldown();

        // รอ 1 เฟรมให้ trigger ถูก consume แล้วรีเซ็ตทิ้ง ป้องกัน trigger ค้าง
        yield return null;
        animator.ResetTrigger(attack.triggerName);

        // คำนวณทิศทางหันหน้าเข้าหาศัตรู
        Vector3 direction = transform.forward;
        if (target != null)
        {
            direction = (target.position - spawnPosition).normalized;
        }
        direction.y = 0; // ล็อคแกน Y

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // Compute dodge target based on configured directions
        Vector3 dodgeTargetPosition = spawnPosition;
        Vector3 offset = Vector3.zero;

        // Forward/backward distance
        if (attack.dodgeDistance > 0f)
        {
            // Use backward if dodgeBackward is true, otherwise forward
            if (attack.dodgeBackward)
                offset -= direction * attack.dodgeDistance;
            else
                offset += direction * attack.dodgeDistance;
        }

        // Additional forward lunge
        if (attack.dodgeForwardLunge > 0f)
        {
            offset += direction * attack.dodgeForwardLunge;
        }

        // Lateral movement
        if (attack.dodgeLateralDistance > 0f)
        {
            if (attack.dodgeLeft)
                offset -= transform.right * attack.dodgeLateralDistance;
            else if (attack.dodgeRight)
                offset += transform.right * attack.dodgeLateralDistance;
        }

        // Apply offset
        dodgeTargetPosition += offset;
        dodgeTargetPosition.y = spawnPosition.y;

        // สไลด์หลบไปยังพิกัด dodgeTargetPosition (ใช้ Lerp ให้เคลื่อนที่ smooth แทนการวาปทันที)
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        if (attack.dodgeDuration > 0f)
        {
            while (elapsed < attack.dodgeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / attack.dodgeDuration;
                Vector3 currentPos = Vector3.Lerp(startPos, dodgeTargetPosition, t);
                currentPos.y = spawnPosition.y;
                transform.position = currentPos;
                yield return null;
            }
            // snap ให้ตรงพิกัดเป๊ะหลัง loop จบ
            transform.position = dodgeTargetPosition;
        }
        else
        {
            // ไม่มี duration → วาปทันที (fallback)
            transform.position = dodgeTargetPosition;
        }

        // รอจนกว่า Animator จะเข้าสู่ state อนิเมชันหลบ
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(attack.animStateName))
        {
            yield return null;
        }

        // รอจนกว่าจะอนิเมชันหลบจะเล่นเสร็จสิ้น (ออกจาก state)
        while (animator.GetCurrentAnimatorStateInfo(0).IsName(attack.animStateName))
        {
            yield return null;
        }

        // 3. ทรานซิชันเข้าสู่สเตทเดินทางกลับ (Return)
        TransitionToState(PlayerState.Return);
        elapsed = 0f;
        Vector3 reachedPosition = transform.position;
        reachedPosition.y = spawnPosition.y;

        // ใช้ dodgeReturnDuration แยกจาก returnDuration ปกติ เพื่อปรับความเร็วกลับหลัง dodge ได้อิสระ
        float activeReturnDuration = dodgeReturnDuration > 0f ? dodgeReturnDuration : returnDuration;
        while (elapsed < activeReturnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / activeReturnDuration;
            Vector3 currentPos = Vector3.Lerp(reachedPosition, spawnPosition, t);
            currentPos.y = spawnPosition.y; // ล็อคแกน Y
            transform.position = currentPos;
            yield return null;
        }
        transform.position = spawnPosition;

        // ปิดการใช้งาน I-Frame (อมตะ) เมื่อทำท่าหลบและกลับมาเสร็จสมบูรณ์
        if (iframeController != null)
        {
            iframeController.SetInvincible(false);
        }
        else if (health != null)
        {
            health.IsInvincible = false;
        }

        // 4. สิ้นสุดการทำงาน กลับสู่สเตท Idle
        TransitionToState(PlayerState.Idle);
    }

    /// <summary>
    /// คอรันทีนจัดการสเตทโจมตีกรณีไม่มีศัตรู (ออกท่าอยู่กับที่) จนอนิเมชั่นเสร็จแล้วจึงกลับสู่ Idle
    /// </summary>
    private IEnumerator StationaryPunchCoroutine(string triggerName, string animStateName)
    {
        yield return null;
        animator.ResetTrigger(triggerName);

        // รอเข้าสู่ state การทำดาเมจของท่านั้นๆ
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(animStateName))
        {
            yield return null;
        }

        // รอจนเล่นจบแอนิเมชัน
        while (animator.GetCurrentAnimatorStateInfo(0).IsName(animStateName))
        {
            yield return null;
        }

        // พลิกกลับมาเป็น Idle
        TransitionToState(PlayerState.Idle);
    }

    private void HandleTakeDamage(float damageAmount)
    {
        PlayDamageCameraShake(damageAmount, "player took damage");

        if (health != null && health.CurrentHealth <= 0f)
        {
            return;
        }

        StopAllCoroutines();
        stunCoroutine = null;
        TransitionToState(PlayerState.Stun);
    }

    private IEnumerator StunTimerCoroutine()
    {
        if (stunDuration > 0f)
        {
            yield return new WaitForSeconds(stunDuration);
        }
        else
        {
            yield return null;
        }

        stunCoroutine = null;
        if (health == null || !health.IsDead)
        {
            TransitionToState(PlayerState.Idle);
        }
    }

    private void ResetAttackTriggers()
    {
        if (animator == null || attacks == null) return;

        for (int i = 0; i < attacks.Length; i++)
        {
            string triggerName = attacks[i].triggerName;
            if (!string.IsNullOrEmpty(triggerName))
            {
                animator.ResetTrigger(triggerName);
            }
        }
    }

    private void PlayDamageCameraShake(float damageAmount, string context)
    {
        if (damageShakeProfile == null || !damageShakeProfile.IsValid)
        {
            Debug.LogWarning($"[PlayerMovement] Cannot play camera shake after {context}; damageShakeProfile is invalid.");
            return;
        }

        if (CameraShake.Instance == null)
        {
            Debug.LogWarning($"[PlayerMovement] Cannot play camera shake after {context}; no CameraShake instance was found.");
            return;
        }

        CameraShake.Instance.Shake(damageShakeProfile);

        if (logDamageShakeEvents)
        {
            Debug.Log($"[PlayerMovement] Camera shake after {context}: damage={damageAmount}, duration={damageShakeProfile.Duration:F2}, magnitude={damageShakeProfile.Magnitude:F2}, rotation={damageShakeProfile.RotationMagnitude:F2}");
        }
    }

    /// <summary>
    /// ค้นหาศัตรูที่มี Tag ว่า "Enemy" ในฉาก
    /// </summary>
    private Transform FindEnemy()
    {
        GameObject enemy = GameObject.FindWithTag("Enemy");
        return enemy != null ? enemy.transform : null;
    }
}
