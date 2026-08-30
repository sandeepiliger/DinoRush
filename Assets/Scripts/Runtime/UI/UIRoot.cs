using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace DinoRush.Runtime
{
    // The canvas everything else attaches to, plus the two things a phone UI cannot skip:
    // resolution-independent scaling and safe-area insets.
    public sealed class UIRoot : MonoBehaviour
    {
        public RectTransform SafeArea { get; private set; }
        public Canvas Canvas { get; private set; }

        private Rect _appliedSafeArea;

        public static UIRoot Create(Transform parent)
        {
            var go = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, worldPositionStays: false);

            var root = go.AddComponent<UIRoot>();
            root.Canvas = go.GetComponent<Canvas>();
            root.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UITheme.ReferenceResolution;
            // Match height rather than width: the design is a tall 390x844 phone layout, and
            // matching width would push the HUD off-screen on shorter aspect ratios. Section 39
            // wants the screen uncluttered, which matters most vertically.
            scaler.matchWidthOrHeight = 1f;

            // uGUI needs an EventSystem for any button to receive input, and there is no scene
            // asset to have placed one — so create it here if the scene didn't bring one.
            //
            // InputSystemUIInputModule, NOT StandaloneInputModule. The legacy module reads
            // UnityEngine.Input internally, and this project's activeInputHandler is set to the
            // Input System package only — so it throws InvalidOperationException on the first
            // pointer poll and no button ever receives a click.
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(parent, worldPositionStays: false);
            }
            else if (EventSystem.current.currentInputModule is StandaloneInputModule legacy)
            {
                // A scene-provided EventSystem (the URP template ships one) will carry the
                // legacy module, which fails the same way. Swap it rather than leaving the UI
                // dead.
                var host = legacy.gameObject;
                Object.Destroy(legacy);
                host.AddComponent<InputSystemUIInputModule>();
            }

            root.SafeArea = UIFactory.CreateRect("SafeArea", go.transform);
            UIFactory.Stretch(root.SafeArea);
            root.ApplySafeArea();

            return root;
        }

        private void Update()
        {
            // Re-applied rather than set once: the safe area changes on rotation, and on Android
            // it can also change after launch as the system bars settle.
            if (Screen.safeArea != _appliedSafeArea) ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            var safe = Screen.safeArea;
            _appliedSafeArea = safe;

            if (Screen.width <= 0 || Screen.height <= 0) return;

            var min = safe.position;
            var max = safe.position + safe.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            SafeArea.anchorMin = min;
            SafeArea.anchorMax = max;
            SafeArea.offsetMin = Vector2.zero;
            SafeArea.offsetMax = Vector2.zero;
        }
    }
}
