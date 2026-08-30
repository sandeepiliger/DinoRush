using System;
using UnityEngine;
using UnityEngine.UI;

namespace DinoRush.Runtime
{
    // Builds uGUI hierarchies in code, for the same reason scenes are built in code
    // (docs/DECISIONS.md D12): hand-authoring Unity YAML from a container with no editor to
    // validate it is how you end up with a project that won't open.
    //
    // Surfaces come from UISprites rather than flat colours, because the design is built from
    // rounded gold-rimmed stone with gradients and depth — a solid-colour Image can only ever
    // look like a flat rectangle.
    //
    // Uses legacy UnityEngine.UI.Text rather than TextMeshPro deliberately. TMP requires a
    // one-time "Import TMP Essential Resources" editor step, and without it every label renders
    // as a missing-glyph box. Legacy Text with Unity's built-in font works with no imported
    // assets at all. TMP is the right long-term choice and arrives with the font pass — the
    // design calls for Bebas Neue and Barlow, which need real font assets and a licence entry
    // per section 57.
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

        public static Image CreateSpriteImage(string name, Transform parent, Sprite sprite, Image.Type type = Image.Type.Sliced)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            return image;
        }

        // A carved stone panel: rounded, gold-rimmed, gradient-filled, sitting on a soft drop
        // shadow. This is the design's core surface — menus, dialogs and pills are all this.
        public static RectTransform CreateStonePanel(string name, Transform parent, int radius = 16)
        {
            var root = CreateRect(name, parent);

            var shadow = CreateSpriteImage("Shadow", root,
                UISprites.RoundedRect(new Color(0f, 0f, 0f, 0.55f), new Color(0f, 0f, 0f, 0.55f),
                    new Color(0f, 0f, 0f, 0.55f), radius, 0));
            Stretch(shadow.rectTransform, -3f, -3f, -6f, -8f);
            shadow.raycastTarget = false;

            var panel = CreateSpriteImage("Panel", root,
                UISprites.RoundedRect(UITheme.PanelTop, UITheme.PanelBottom, UITheme.GoldRim, radius));
            Stretch(panel.rectTransform);

            return root;
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

        // The design's headline treatment: a gold vertical gradient over a hard offset shadow,
        // which together read as embossed metal rather than flat yellow text.
        public static Text CreateHeadline(
            string name, Transform parent, string content, int size,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var text = CreateLabel(name, parent, content, size, Color.white, alignment, FontStyle.Bold);

            var gradient = text.gameObject.AddComponent<GradientText>();
            gradient.TopColor = UITheme.HeadlineTop;
            gradient.BottomColor = UITheme.HeadlineBottom;

            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.24f, 0.09f, 0.02f, 0.95f);
            shadow.effectDistance = new Vector2(0f, -3f);

            return text;
        }

        // The design's chunky extruded button: a bright rounded face sitting on a darker depth
        // block. Pressing drives the face down into the depth, which is what sells the 3D read.
        public static Button CreateChunkyButton(
            string name, Transform parent, string label, Color faceTop, Color faceBottom, Color depth,
            Color textColor, int fontSize, Action onClick)
        {
            var root = CreateRect(name, parent);

            var depthImage = CreateSpriteImage("Depth", root,
                UISprites.RoundedRect(depth, depth, depth, 14, 0));
            Stretch(depthImage.rectTransform);

            var face = CreateSpriteImage("Face", root,
                UISprites.RoundedRect(faceTop, faceBottom, UITheme.RimHighlight, 14, 2));
            // The face floats above the depth block; the gap underneath is the extrusion.
            Stretch(face.rectTransform, 0, 0, 0, 7f);

            var text = CreateLabel("Label", face.transform, label, fontSize, textColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None; // the press animation drives the face instead

            var press = root.gameObject.AddComponent<ButtonPressEffect>();
            press.Face = face.rectTransform;

            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        // A rounded stat pill — the coin chips along the top of the design's menu and HUD.
        public static Text CreatePill(string name, Transform parent, string content, Color accent, out Image background)
        {
            var root = CreateStonePanel(name, parent, radius: 18);

            var dot = CreateSpriteImage("Accent", root, UISprites.RoundedRect(accent, accent, accent, 32, 0));
            dot.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            dot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            dot.rectTransform.pivot = new Vector2(0f, 0.5f);
            dot.rectTransform.anchoredPosition = new Vector2(9f, 0f);
            dot.rectTransform.sizeDelta = new Vector2(16f, 16f);
            dot.raycastTarget = false;

            var text = CreateLabel("Value", root, content, UITheme.SizeBody, UITheme.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Stretch(text.rectTransform, 31f, 10f, 0f, 0f);

            background = root.Find("Panel").GetComponent<Image>();
            return text;
        }

        // A soft glow, used behind headlines and focal elements.
        public static Image CreateGlow(string name, Transform parent, Color colour)
        {
            var glow = CreateSpriteImage(name, parent, UISprites.RadialGlow(colour), Image.Type.Simple);
            glow.raycastTarget = false;
            return glow;
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
