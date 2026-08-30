using DinoRush.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DinoRush.Runtime
{
    // Translates raw input into a PlayerIntent. Uses the Input System package API rather than
    // legacy UnityEngine.Input because this project's activeInputHandler is set to the new
    // system only — legacy Input compiles fine but throws at runtime.
    //
    // Gesture model (CLAUDE.md section 3): tap = jump, swipe down = duck.
    //
    // A tap resolves on finger-release rather than on touch-down. Touch-down cannot be used
    // for jump: a swipe-down begins with a touch-down too, so firing there would make every
    // duck attempt jump first, and PlayerMotor deliberately ignores ducking in mid-air. The
    // cost is the ~50-80ms of a finger lift; the duck path stays instant, firing the moment
    // the swipe threshold is crossed rather than waiting for release.
    public sealed class RunInputReader
    {
        private const float SwipeThresholdPixels = 60f;
        private const float TapMaxTravelPixels = 40f;

        private bool _tracking;
        private bool _gestureConsumed;
        private Vector2 _startPosition;

        public PlayerIntent Read()
        {
            var keyboardIntent = ReadKeyboard();
            if (keyboardIntent != PlayerIntent.None) return keyboardIntent;

            return ReadPointer();
        }

        // Editor/desktop convenience: lets the run be played and debugged without a device.
        private static PlayerIntent ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return PlayerIntent.None;

            if (keyboard.spaceKey.wasPressedThisFrame ||
                keyboard.upArrowKey.wasPressedThisFrame ||
                keyboard.wKey.wasPressedThisFrame)
                return PlayerIntent.Jump;

            if (keyboard.downArrowKey.wasPressedThisFrame ||
                keyboard.sKey.wasPressedThisFrame)
                return PlayerIntent.Duck;

            return PlayerIntent.None;
        }

        // Touchscreen and mouse share a press/position/release shape, so one path covers both:
        // touch on device, mouse drag in the editor.
        private PlayerIntent ReadPointer()
        {
            if (!TryGetPointer(out bool pressedThisFrame, out bool releasedThisFrame, out bool isPressed, out Vector2 position))
                return PlayerIntent.None;

            if (pressedThisFrame)
            {
                _tracking = true;
                _gestureConsumed = false;
                _startPosition = position;
                return PlayerIntent.None;
            }

            if (!_tracking) return PlayerIntent.None;

            Vector2 travel = position - _startPosition;

            if (isPressed && !_gestureConsumed && travel.y <= -SwipeThresholdPixels)
            {
                // Fire the duck as soon as the gesture is unambiguous, without waiting for release.
                _gestureConsumed = true;
                return PlayerIntent.Duck;
            }

            if (releasedThisFrame)
            {
                _tracking = false;
                bool wasConsumed = _gestureConsumed;
                _gestureConsumed = false;

                if (!wasConsumed && travel.magnitude <= TapMaxTravelPixels)
                    return PlayerIntent.Jump;
            }

            return PlayerIntent.None;
        }

        private static bool TryGetPointer(out bool pressed, out bool released, out bool isPressed, out Vector2 position)
        {
            var touch = Touchscreen.current?.primaryTouch;
            if (touch != null && (touch.press.isPressed || touch.press.wasReleasedThisFrame))
            {
                pressed = touch.press.wasPressedThisFrame;
                released = touch.press.wasReleasedThisFrame;
                isPressed = touch.press.isPressed;
                position = touch.position.ReadValue();
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                pressed = mouse.leftButton.wasPressedThisFrame;
                released = mouse.leftButton.wasReleasedThisFrame;
                isPressed = mouse.leftButton.isPressed;
                position = mouse.position.ReadValue();
                return pressed || released || isPressed;
            }

            pressed = released = isPressed = false;
            position = default;
            return false;
        }

        public void Reset()
        {
            _tracking = false;
            _gestureConsumed = false;
        }

        // True when the player is making any "continue" gesture — used by the game-over screen,
        // where any tap or key restarts rather than meaning jump or duck specifically.
        public static bool AnyConfirmPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;

            var touch = Touchscreen.current?.primaryTouch;
            return touch != null && touch.press.wasPressedThisFrame;
        }
    }
}
