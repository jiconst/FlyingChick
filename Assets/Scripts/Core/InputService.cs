using UnityEngine;
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

        // Keyboard-only variant of IsPointerDownThisFrame's space-bar branch.
        // M7: once the start screen's tap-anywhere-to-start became a real
        // UGUI full-screen button, mouse/touch already reach it through
        // normal UI click routing -- polling IsPointerDownThisFrame there
        // too would fire BeginRun() twice for the same click. Space bar has
        // no UGUI click path, so it still needs to be polled directly.
        public static bool IsSpaceDownThisFrame()
        {
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        // Unity screen space (origin bottom-left, y-up). Used by StartScreen
        // (M6) to tell a "start the run" tap from a tap on a UI button --
        // OnGUI's Rect space is y-down, so callers need
        // (pos.x, Screen.height - pos.y) to compare against a GUI Rect.
        public static Vector2 PointerPosition()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return Touchscreen.current.primaryTouch.position.ReadValue();
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
            return Vector2.zero;
        }
    }
}
