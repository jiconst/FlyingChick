using UnityEngine.InputSystem;

namespace FlyingChick
{
    // Minimal touch/mouse/keyboard input abstraction using the new Input
    // System package (this project's Active Input Handling is set to
    // "Input System Package", so legacy UnityEngine.Input is unavailable).
    // Spacebar is included per spec: desktop testing uses mouse click or
    // space, mobile uses touch.
    public static class InputService
    {
        public static bool IsPointerHeld()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return true;
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                return true;
            return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        }

        public static bool IsPointerDownThisFrame()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        }
    }
}
