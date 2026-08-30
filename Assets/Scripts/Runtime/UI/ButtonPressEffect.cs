using UnityEngine;
using UnityEngine.EventSystems;

namespace DinoRush.Runtime
{
    // Drives a chunky button's face down into its depth block while held.
    //
    // Unity's built-in colour tint can't express this: the design's buttons are extruded solids,
    // and the press reads as the face physically sinking, not as the button changing shade.
    // Moving the face by the extrusion height and back is the whole effect.
    public sealed class ButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private const float PressDepth = 5f;

        public RectTransform Face;

        private Vector2 _restOffsetMin;
        private Vector2 _restOffsetMax;
        private bool _captured;
        private bool _pressed;

        private void Awake() => Capture();

        private void Capture()
        {
            if (Face == null || _captured) return;
            _restOffsetMin = Face.offsetMin;
            _restOffsetMax = Face.offsetMax;
            _captured = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Capture();
            if (Face == null || _pressed) return;

            _pressed = true;
            Face.offsetMin = _restOffsetMin + new Vector2(0f, -PressDepth);
            Face.offsetMax = _restOffsetMax + new Vector2(0f, -PressDepth);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (Face == null || !_pressed) return;

            _pressed = false;
            Face.offsetMin = _restOffsetMin;
            Face.offsetMax = _restOffsetMax;
        }

        // A button hidden or disabled mid-press never receives its pointer-up, and would come
        // back still sunk.
        private void OnDisable()
        {
            if (Face != null && _pressed)
            {
                _pressed = false;
                Face.offsetMin = _restOffsetMin;
                Face.offsetMax = _restOffsetMax;
            }
        }
    }
}
