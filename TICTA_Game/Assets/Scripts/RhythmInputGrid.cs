using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RhythmInputGrid : MonoBehaviour
{
    private const int SlotCount = 9;

    [SerializeField] private GameObject[] slots = new GameObject[SlotCount];
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

    public UnityEvent<int> OnSlotPressed => onSlotPressed;
    public UnityEvent<int> OnSlotHeld => onSlotHeld;
    public UnityEvent<int> OnSlotReleased => onSlotReleased;

    private void Awake()
    {
        EnsureInspectorArraySizes();
        AutoFillSlotsFromChildren();
        CacheDefaultColors();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
        {
            if (WasSlotKeyPressed(keyboard, slotIndex))
            {
                PressSlot(slotIndex);
            }

            if (IsSlotKeyPressed(keyboard, slotIndex))
            {
                HoldSlot(slotIndex);
            }

            if (WasSlotKeyReleased(keyboard, slotIndex))
            {
                ReleaseSlot(slotIndex);
            }

        }
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

    public bool IsSlotHeld(int slotIndex)
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && IsValidSlotIndex(slotIndex) && IsSlotKeyPressed(keyboard, slotIndex);
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

    private static bool WasSlotKeyPressed(Keyboard keyboard, int slotIndex)
    {
        switch (slotIndex)
        {
            case 0:
                return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
            case 1:
                return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
            case 2:
                return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
            case 3:
                return keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame;
            case 4:
                return keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame;
            case 5:
                return keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame;
            case 6:
                return keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame;
            case 7:
                return keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame;
            case 8:
                return keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame;
            default:
                return false;
        }
    }

    private static bool WasSlotKeyReleased(Keyboard keyboard, int slotIndex)
    {
        switch (slotIndex)
        {
            case 0:
                return keyboard.digit1Key.wasReleasedThisFrame || keyboard.numpad1Key.wasReleasedThisFrame;
            case 1:
                return keyboard.digit2Key.wasReleasedThisFrame || keyboard.numpad2Key.wasReleasedThisFrame;
            case 2:
                return keyboard.digit3Key.wasReleasedThisFrame || keyboard.numpad3Key.wasReleasedThisFrame;
            case 3:
                return keyboard.digit4Key.wasReleasedThisFrame || keyboard.numpad4Key.wasReleasedThisFrame;
            case 4:
                return keyboard.digit5Key.wasReleasedThisFrame || keyboard.numpad5Key.wasReleasedThisFrame;
            case 5:
                return keyboard.digit6Key.wasReleasedThisFrame || keyboard.numpad6Key.wasReleasedThisFrame;
            case 6:
                return keyboard.digit7Key.wasReleasedThisFrame || keyboard.numpad7Key.wasReleasedThisFrame;
            case 7:
                return keyboard.digit8Key.wasReleasedThisFrame || keyboard.numpad8Key.wasReleasedThisFrame;
            case 8:
                return keyboard.digit9Key.wasReleasedThisFrame || keyboard.numpad9Key.wasReleasedThisFrame;
            default:
                return false;
        }
    }

    private static bool IsSlotKeyPressed(Keyboard keyboard, int slotIndex)
    {
        switch (slotIndex)
        {
            case 0:
                return keyboard.digit1Key.isPressed || keyboard.numpad1Key.isPressed;
            case 1:
                return keyboard.digit2Key.isPressed || keyboard.numpad2Key.isPressed;
            case 2:
                return keyboard.digit3Key.isPressed || keyboard.numpad3Key.isPressed;
            case 3:
                return keyboard.digit4Key.isPressed || keyboard.numpad4Key.isPressed;
            case 4:
                return keyboard.digit5Key.isPressed || keyboard.numpad5Key.isPressed;
            case 5:
                return keyboard.digit6Key.isPressed || keyboard.numpad6Key.isPressed;
            case 6:
                return keyboard.digit7Key.isPressed || keyboard.numpad7Key.isPressed;
            case 7:
                return keyboard.digit8Key.isPressed || keyboard.numpad8Key.isPressed;
            case 8:
                return keyboard.digit9Key.isPressed || keyboard.numpad9Key.isPressed;
            default:
                return false;
        }
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
