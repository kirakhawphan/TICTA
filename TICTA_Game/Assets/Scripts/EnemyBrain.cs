using UnityEngine;
using System.Collections;

// กำหนด State ของ AI
public enum EnemyState
{
    Idle,
    Stun
}

public class EnemyBrain : MonoBehaviour
{
    [Header("State Settings")]
    [SerializeField] private EnemyState currentState = EnemyState.Idle;

    [Header("Stun Settings")]
    [SerializeField] private float stunDuration = 1.0f; // ระยะเวลาที่ศัตรูจะติดมึนงง (วินาที)
    [SerializeField] private string stunTriggerName = "Stun"; // ชื่อพารามิเตอร์ใน Animator สำหรับท่าโดนดาเมจ/มึนงง

    private Animator animator;
    private Health health;
    private Coroutine stunCoroutine;

    // Property เพื่อให้ภายนอกตรวจสอบ State ปัจจุบันได้
    public EnemyState CurrentState => currentState;

    void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponent<Health>();
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
            case EnemyState.Stun:
                UpdateStun();
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
                // ตัวอย่าง: ถ้ามีอนิเมชั่นวิ่งหรือขยับก็หยุดตรงนี้
                break;

            case EnemyState.Stun:
                // เล่นแอนิเมชันมึนงง (ถ้าผู้ใช้เซ็ต Trigger ใน Animator ไว้)
                if (animator != null)
                {
                    animator.SetTrigger(stunTriggerName);
                }

                // สั่งให้นับเวลาถอยหลังเพื่อหลุดพ้นจากสถานะ Stun
                if (stunCoroutine != null)
                {
                    StopCoroutine(stunCoroutine);
                }
                stunCoroutine = StartCoroutine(StunTimerCoroutine());
                break;
        }
    }

    // พฤติกรรมที่ประมวลผลต่อเนื่องใน Update
    private void UpdateIdle()
    {
        // พื้นที่เขียนพฤติกรรมเมื่ออยู่นิ่งๆ เช่น มองหาเป้าหมาย, เดินลาดตระเวน (Patrol)
    }

    private void UpdateStun()
    {
        // พื้นที่เขียนพฤติกรรมเมื่อติดมึนงง เช่น ห้ามเดิน, หมุนตัวมึน
    }

    // สิ่งที่ทำก่อนจะย้ายออกจากสถานะเดิม
    private void ExitState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Idle:
                break;
            case EnemyState.Stun:
                break;
        }
    }

    // เมื่อได้รับความเสียหายจาก Health.cs
    private void HandleTakeDamage(float damageAmount)
    {
        // ถ้าได้รับดาเมจให้เปลี่ยนเป็นสถานะ Stun ทันที
        TransitionToState(EnemyState.Stun);
    }

    // คอร์รูทีนสำหรับนับเวลาถอยหลังการหายมึนงง (Stun)
    private IEnumerator StunTimerCoroutine()
    {
        yield return new WaitForSeconds(stunDuration);

        // ตรวจสอบว่าถ้ายังไม่ตาย ให้ฟื้นกลับไปสู่สถานะ Idle
        if (health != null && !health.IsDead)
        {
            TransitionToState(EnemyState.Idle);
        }
    }
}
