using System;
using UnityEngine;
using UnityEngine.UI;

namespace DinoRush.Runtime
{
    // Builds uGUI hierarchies in code, for the same reason scenes are built in code
    // (docs/DECISIONS.md D12): hand-authoring Unity YAML from a container with no editor to
    // validate it is how you end up with a project that won't open.
    //
    // Uses legacy UnityEngine.UI.Text rather than TextMeshPro deliberately. TMP requires a
    // one-time "Import TMP Essential Resources" editor step, and without it every label renders
    // as a missing-glyph box — a failure that only shows up when someone opens the editor.
    // Legacy Text with Unity's built-in font works with no imported assets at all. TMP is the
    // right long-term choice and can be swapped in once the font pass happens (the design calls
    // for Bebas Neue and Barlow, which need real font assets and a licence entry per section 57).
    public static class UIFactory
    {
        private static Font _font;

        // Arial.ttf was removed as a builtin; LegacyRuntime.ttf is its documented replacement
        // and is always present, so this needs nothing added to the project.
        private static Font Font =>
            _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, worldPositionStays: false);
            rect.localScale = Vector3.one;
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static Image CreatePanel(string name, Transform parent, Color color)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        // A carved-stone panel: dark fill inside a gold rim. The design draws this as a border
        // plus an inset highlight; two nested images is the cheapest faithful approximation and
        // costs one extra draw call rather than a custom shader.
        public static RectTransform CreateStonePanel(string name, Transform parent, float rimThickness = 2f)
        {
            var rim = CreatePanel(name, parent, UITheme.GoldRim);
            var fill = CreatePanel("Fill", rim.transform, UITheme.PanelBottom);
            Stretch(fill.rectTransform, rimThickness, rimThickness, rimThickness, rimThickness);
            return rim.rectTransform;
        }

        public static Text CreateLabel(
            string name, Transform parent, string content, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false; // labels must never eat taps meant for buttons
            return text;
        }

        // The design's chunky extruded button: a bright face sitting on a darker "depth" block.
        // Pressing pushes the face down into the depth, which is what sells the 3D read without
        // any art. onClick is wired here so callers never touch Button internals.
        public static Button CreateChunkyButton(
            string name, Transform parent, string label, Color face, Color depth, Color textColor,
            int fontSize, Action onClick)
        {
            var root = CreateRect(name, parent);

            var shadow = CreatePanel("Depth", root, depth);
            Stretch(shadow.rectTransform);

            var faceImage = CreatePanel("Face", root, face);
            Stretch(faceImage.rectTransform, 0, 0, 0, 6f); // 6px of depth showing beneath

            var text = CreateLabel("Label", faceImage.transform, label, fontSize, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = faceImage;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        // A rounded stat pill — the coin/gem chips along the top of the design's menu and HUD.
        public static Text CreatePill(string name, Transform parent, string content, Color accent, out Image background)
        {
            var rect = CreateStonePanel(name, parent);

            var dot = CreatePanel("Accent", rect, accent);
            dot.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            dot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            dot.rectTransform.pivot = new Vector2(0f, 0.5f);
            dot.rectTransform.anchoredPosition = new Vector2(8f, 0f);
            dot.rectTransform.sizeDelta = new Vector2(14f, 14f);

            var text = CreateLabel("Value", rect, content, UITheme.SizeBody, UITheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            Stretch(text.rectTransform, 28f, 10f, 0f, 0f);

            background = rect.GetComponent<Image>();
            return text;
        }

        public static void SetAnchoredBox(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
