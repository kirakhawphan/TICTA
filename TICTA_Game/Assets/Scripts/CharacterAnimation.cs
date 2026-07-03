using UnityEngine;

public class CharacterAnimation : MonoBehaviour
{
    private Animator animator;

    // ใส่ชื่อ State ของอนิเมชั่นใน Animator Controller ที่ต้องการให้เล่นตอนเริ่มเกม
    [SerializeField] private string animationStateName = "Punching";

    void Start()
    {
        // ดึง Component Animator
        animator = GetComponent<Animator>();

        if (animator != null)
        {
            // สั่งรันอนิเมชั่นทันทีที่เริ่มเกม
            animator.Play(animationStateName);
        }
        else
        {
            Debug.LogError("ไม่พบ Animator Component! กรุณาเพิ่ม Animator ก่อนรันสคริปต์");
        }
    }
}
