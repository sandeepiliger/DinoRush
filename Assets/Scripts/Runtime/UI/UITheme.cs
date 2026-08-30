using UnityEngine;

namespace DinoRush.Runtime
{
    // The palette from the design canvas (Dino_Rush_Game_UI_v2), which states its own key
    // values: STONE #1A130E, GOLD RIM #6B4A1F, LAVA #FF8B32. Everything else here is sampled
    // from the artboards so the built UI and the design don't drift apart.
    //
    // Colours live in one place because the design is a carved-stone-and-gold system, not a set
    // of independent screens — changing the rim colour has to change every rim at once.
    public static class UITheme
    {
        // Surfaces
        public static readonly Color Stone = Hex("1A130E");
        public static readonly Color PanelTop = Hex("2B2118");
        public static readonly Color PanelBottom = Hex("170F0B");
        public static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.72f);
        public static readonly Color GoldRim = Hex("6B4A1F");
        public static readonly Color GoldRimBright = Hex("FFC061");

        // Text
        public static readonly Color TextPrimary = Hex("FFE6C4");
        public static readonly Color TextSecondary = new Color32(0xFF, 0xD6, 0xA0, 0x99);
        public static readonly Color TextOnGold = Hex("3A1A06");

        // Accents
        public static readonly Color Lava = Hex("FF8B32");
        public static readonly Color GoldLight = Hex("FFD07A");
        public static readonly Color GoldDeep = Hex("E2611A");
        public static readonly Color Danger = Hex("B3350F");
        public static readonly Color DangerDeep = Hex("7A1F06");
        public static readonly Color Coin = Hex("F2B13C");
        public static readonly Color Success = Hex("7FD66A");

        // The design is drawn at 390x844. Using it as the CanvasScaler reference means every
        // size below is the same number the artboards use, so layouts can be read straight off
        // the design rather than re-derived.
        public static readonly Vector2 ReferenceResolution = new Vector2(390f, 844f);

        // Type scale, matching the artboards' font sizes.
        public const int SizeDisplay = 52;
        public const int SizeTitle = 34;
        public const int SizeHeading = 22;
        public const int SizeBody = 15;
        public const int SizeCaption = 11;

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.magenta;
        }
    }
}
