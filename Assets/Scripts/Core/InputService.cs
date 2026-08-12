using UnityEngine.InputSystem;

namespace FlyingChick
{
    // Minimal touch/mouse abstraction so gameplay code doesn't care which one
    // is active. Uses the new Input System package (this project's Active
    // Input Handling is set to "Input System Package", so the legacy
    // UnityEngine.Input class is unavailable). Works with a touchscreen on
    // device and falls back to the mouse in the Editor.
    public static class InputService
    {
        public static bool IsPointerHeld()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return true;
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
        }

        public static bool IsPointerDownThisFrame()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return true;
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }
    }
}
