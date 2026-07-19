using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class RhythmNote : MonoBehaviour
{
    private enum HitInputPhase
    {
        Pressed,
        Held,
        Released
    }

    [SerializeField] private RhythmInputGrid inputGrid;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Vector3 moveDirection = Vector3.forward;
    [SerializeField] private HitInputPhase destroyInputPhase = HitInputPhase.Held;

    private int overlappingSlotIndex = -1;
    private bool isDestroying;

    private void Awake()
    {
        if (inputGrid == null) {
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
        if (inputGrid != null)
        {
            inputGrid.OnSlotPressed.AddListener(HandleSlotPressed);
            inputGrid.OnSlotHeld.AddListener(HandleSlotHeld);
            inputGrid.OnSlotReleased.AddListener(HandleSlotReleased);
        }
    }

    private void OnDisable()
    {
        if (inputGrid != null)
        {
            inputGrid.OnSlotPressed.RemoveListener(HandleSlotPressed);
            inputGrid.OnSlotHeld.RemoveListener(HandleSlotHeld);
            inputGrid.OnSlotReleased.RemoveListener(HandleSlotReleased);
        }
    }

    private void Update()
    {
        if (moveDirection == Vector3.zero || Mathf.Approximately(moveSpeed, 0f))
        {
            return;
        }

        transform.position += moveDirection.normalized * moveSpeed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryStartOverlap(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        TryEndOverlap(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryStartOverlap(collision.gameObject);
    }

    private void OnCollisionExit(Collision collision)
    {
        TryEndOverlap(collision.gameObject);
    }

    private void HandleSlotPressed(int slotIndex)
    {
        TryDestroyForSlot(slotIndex, HitInputPhase.Pressed);
    }

    private void HandleSlotHeld(int slotIndex)
    {
        TryDestroyForSlot(slotIndex, HitInputPhase.Held);
    }

    private void HandleSlotReleased(int slotIndex)
    {
        TryDestroyForSlot(slotIndex, HitInputPhase.Released);
    }

    private void TryDestroyForSlot(int slotIndex, HitInputPhase inputPhase)
    {
        if (isDestroying || slotIndex != overlappingSlotIndex)
        {
            return;
        }

        if (inputPhase != destroyInputPhase)
        {
            return;
        }

        isDestroying = true;
        Destroy(gameObject);
    }

    private void TryStartOverlap(GameObject otherObject)
    {
        if (inputGrid == null || isDestroying)
        {
            return;
        }

        if (inputGrid.TryGetSlotIndex(otherObject, out int slotIndex))
        {
            overlappingSlotIndex = slotIndex;

            if (inputGrid.IsSlotHeld(slotIndex))
            {
                TryDestroyForSlot(slotIndex, HitInputPhase.Held);
            }
        }
    }

    private void TryEndOverlap(GameObject otherObject)
    {
        if (inputGrid == null || !inputGrid.TryGetSlotIndex(otherObject, out int slotIndex))
        {
            return;
        }

        if (slotIndex == overlappingSlotIndex)
        {
            overlappingSlotIndex = -1;
        }
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
        moveSpeed = Mathf.Max(0f, moveSpeed);
    }
}
