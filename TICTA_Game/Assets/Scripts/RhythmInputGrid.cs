using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RhythmInputGrid : MonoBehaviour
{
    public const int SlotCount = 9;

    [SerializeField] private GameObject[] slots = new GameObject[SlotCount];
    [SerializeField] private Camera inputCamera;
    [SerializeField] private LayerMask slotLayerMask = Physics.DefaultRaycastLayers;
    [SerializeField] private float raycastDistance = 100f;
    [SerializeField] private Color[] pressedColors = new Color[SlotCount]
    {
        Color.red,
        new Color(1f, 0.5f, 0f),
        Color.yellow,
        Color.green,
        Color.cyan,
        Color.blue,
        new Color(0.5f, 0f, 1f),
        Color.magenta,
        Color.white
    };
    [SerializeField] private float resetDelay = 0.08f;
    [SerializeField] private UnityEvent<int> onSlotPressed = new UnityEvent<int>();
    [SerializeField] private UnityEvent<int> onSlotHeld = new UnityEvent<int>();
    [SerializeField] private UnityEvent<int> onSlotReleased = new UnityEvent<int>();

    private readonly Color[] defaultColors = new Color[SlotCount];
    private readonly System.Collections.Generic.List<RaycastResult> uiRaycastResults = new System.Collections.Generic.List<RaycastResult>();
    private int activeSlotIndex = -1;

    public UnityEvent<int> OnSlotPressed => onSlotPressed;
    public UnityEvent<int> OnSlotHeld => onSlotHeld;
    public UnityEvent<int> OnSlotReleased => onSlotReleased;
    public int ActiveSlotIndex => activeSlotIndex;

    private void Awake()
    {
        EnsureInspectorArraySizes();
        AutoFillSlotsFromChildren();
        CacheDefaultColors();
    }

    private void Update()
    {
        UpdateMouseDragInput();
    }

    private void OnDisable()
    {
        ReleaseActiveSlot();
    }

    private void UpdateMouseDragInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            ReleaseActiveSlot();
            return;
        }

        int hoveredSlotIndex = GetHoveredSlotIndex(mouse.position.ReadValue());
        if (hoveredSlotIndex != activeSlotIndex)
        {
            ReleaseActiveSlot();

            if (IsValidSlotIndex(hoveredSlotIndex))
            {
                activeSlotIndex = hoveredSlotIndex;
                PressSlot(activeSlotIndex);
            }
        }

        if (IsValidSlotIndex(activeSlotIndex))
        {
            HoldSlot(activeSlotIndex);
        }
    }

    private int GetHoveredSlotIndex(Vector2 screenPosition)
    {
        if (TryGetHoveredUiSlotIndex(screenPosition, out int uiSlotIndex))
        {
            return uiSlotIndex;
        }

        Camera rayCamera = inputCamera != null ? inputCamera : Camera.main;
        if (rayCamera == null)
        {
            return -1;
        }

        Ray ray = rayCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, raycastDistance, slotLayerMask, QueryTriggerInteraction.Collide);
        int closestSlotIndex = -1;
        float closestDistance = float.PositiveInfinity;

        for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
        {
            RaycastHit hit = hits[hitIndex];
            if (hit.distance >= closestDistance)
            {
                continue;
            }

            if (TryGetSlotIndex(hit.collider.gameObject, out int slotIndex))
            {
                closestSlotIndex = slotIndex;
                closestDistance = hit.distance;
            }
        }

        return closestSlotIndex;
    }

    private bool TryGetHoveredUiSlotIndex(Vector2 screenPosition, out int slotIndex)
    {
        slotIndex = -1;
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerEventData, uiRaycastResults);

        for (int resultIndex = 0; resultIndex < uiRaycastResults.Count; resultIndex++)
        {
            GameObject hitObject = uiRaycastResults[resultIndex].gameObject;
            if (TryGetSlotIndex(hitObject, out slotIndex))
            {
                return true;
            }
        }

        return false;
    }

    private void ReleaseActiveSlot()
    {
        if (!IsValidSlotIndex(activeSlotIndex))
        {
            return;
        }

        int releasedSlotIndex = activeSlotIndex;
        activeSlotIndex = -1;
        ReleaseSlot(releasedSlotIndex);
    }

    public void PressSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            return;
        }

        SetSlotColor(slotIndex, pressedColors[slotIndex]);
        onSlotPressed.Invoke(slotIndex);
    }

    public void HoldSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            return;
        }

        onSlotHeld.Invoke(slotIndex);
    }

    public void ReleaseSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            return;
        }

        SetSlotColor(slotIndex, defaultColors[slotIndex]);
        onSlotReleased.Invoke(slotIndex);
    }

    public void PressSlot(GameObject slotObject)
    {
        int slotIndex = FindSlotIndex(slotObject);
        if (IsValidSlotIndex(slotIndex))
        {
            PressSlot(slotIndex);
        }
    }

    public void ReleaseSlot(GameObject slotObject)
    {
        int slotIndex = FindSlotIndex(slotObject);
        if (IsValidSlotIndex(slotIndex))
        {
            ReleaseSlot(slotIndex);
        }
    }

    public void HoldSlot(GameObject slotObject)
    {
        int slotIndex = FindSlotIndex(slotObject);
        if (IsValidSlotIndex(slotIndex))
        {
            HoldSlot(slotIndex);
        }
    }

    private void AutoFillSlotsFromChildren()
    {
        int childCount = Mathf.Min(transform.childCount, SlotCount);
        for (int childIndex = 0; childIndex < childCount; childIndex++)
        {
            GameObject child = transform.GetChild(childIndex).gameObject;
            if (!ContainsSlot(child, childIndex))
            {
                slots[childIndex] = child;
            }
        }
    }

    private void CacheDefaultColors()
    {
        for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
        {
            defaultColors[slotIndex] = GetSlotColor(slots[slotIndex]);
        }
    }

    private int FindSlotIndex(GameObject slotObject)
    {
        if (slotObject == null)
        {
            return -1;
        }

        for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
        {
            GameObject slot = slots[slotIndex];
            if (slot == null)
            {
                continue;
            }

            if (slot == slotObject || slotObject.transform.IsChildOf(slot.transform))
            {
                return slotIndex;
            }
        }

        return -1;
    }

    public bool TryGetSlotIndex(GameObject slotObject, out int slotIndex)
    {
        slotIndex = FindSlotIndex(slotObject);
        return IsValidSlotIndex(slotIndex);
    }

    public bool TryGetSlotTransform(int slotIndex, out Transform slotTransform)
    {
        slotTransform = null;
        if (!IsValidSlotIndex(slotIndex) || slots[slotIndex] == null)
        {
            return false;
        }

        slotTransform = slots[slotIndex].transform;
        return true;
    }

    public bool IsSlotHeld(int slotIndex)
    {
        return IsValidSlotIndex(slotIndex) && slotIndex == activeSlotIndex;
    }

    private void SetSlotColor(int slotIndex, Color color)
    {
        GameObject slot = slots[slotIndex];
        if (slot == null)
        {
            return;
        }

        if (slot.TryGetComponent(out Graphic graphic))
        {
            graphic.color = color;
            return;
        }

        if (slot.TryGetComponent(out SpriteRenderer spriteRenderer))
        {
            spriteRenderer.color = color;
            return;
        }

        if (slot.TryGetComponent(out Renderer renderer))
        {
            renderer.material.color = color;
        }
    }

    private static Color GetSlotColor(GameObject slot)
    {
        if (slot == null)
        {
            return Color.white;
        }

        if (slot.TryGetComponent(out Graphic graphic))
        {
            return graphic.color;
        }

        if (slot.TryGetComponent(out SpriteRenderer spriteRenderer))
        {
            return spriteRenderer.color;
        }

        if (slot.TryGetComponent(out Renderer renderer))
        {
            return renderer.sharedMaterial != null ? renderer.sharedMaterial.color : Color.white;
        }

        return Color.white;
    }

    private bool ContainsSlot(GameObject slotObject, int slotsToCheck)
    {
        for (int slotIndex = 0; slotIndex < slotsToCheck; slotIndex++)
        {
            if (slots[slotIndex] == slotObject)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < SlotCount;
    }

    private void Reset()
    {
        EnsureInspectorArraySizes();
        AutoFillSlotsFromChildren();
    }

    private void OnValidate()
    {
        EnsureInspectorArraySizes();
        resetDelay = Mathf.Max(0f, resetDelay);
        raycastDistance = Mathf.Max(0f, raycastDistance);
    }

    private void EnsureInspectorArraySizes()
    {
        if (slots == null || slots.Length != SlotCount)
        {
            GameObject[] resizedSlots = new GameObject[SlotCount];
            if (slots != null)
            {
                for (int i = 0; i < Mathf.Min(slots.Length, SlotCount); i++)
                {
                    resizedSlots[i] = slots[i];
                }
            }

            slots = resizedSlots;
        }

        if (pressedColors == null || pressedColors.Length != SlotCount)
        {
            Color[] resizedColors = new Color[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                resizedColors[i] = i < pressedColors?.Length ? pressedColors[i] : Color.white;
            }

            pressedColors = resizedColors;
        }
    }
}
