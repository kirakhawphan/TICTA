using UnityEngine;
using UnityEngine.InputSystem; // นำเข้า New Input System

public class PlayerMovement : MonoBehaviour
{
    private Animator animator;

    [Header("Animation Settings")]
    [SerializeField] private string punchTriggerName = "Punch";

    [Header("Cooldown Settings")]
    // เรียกใช้งานโมดูลคูลดาวน์ที่เราสร้างแยกไว้ (จะโชว์ช่องให้ปรับเวลาใน Inspector ของ Unity)
    [SerializeField] private Cooldown punchCooldown;

    void Start()
    {
        animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError("ไม่พบ Animator Component!");
        }
    }

    void Update()
    {
        if (animator == null) return;

        // ตรวจสอบการคลิกเมาส์ซ้าย และตรวจสอบว่าคูลดาวน์เสร็จแล้วหรือยัง
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (punchCooldown.IsReady)
            {
                // เล่นอนิเมชั่นต่อย
                animator.SetTrigger(punchTriggerName);

                // เริ่มนับคูลดาวน์รอบใหม่
                punchCooldown.StartCooldown();
                
                Debug.Log($"ต่อยสำเร็จ! เริ่มคูลดาวน์เป็นเวลา: {punchCooldown.CooldownTime} วินาที");
            }
            else
            {
                Debug.Log($"ยังต่อยไม่ได้! ติดคูลดาวน์เหลืออีก: {punchCooldown.TimeRemaining:F2} วินาที");
            }
        }
    }
}
