using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // The conversion boundary between Core's engine-free PaletteColor and UnityEngine.Color.
    // Core defines biome identity as data (docs/DECISIONS.md D9); this is the only place that
    // data becomes something Unity can render.
    public static class PaletteExtensions
    {
        public static Color ToColor(this PaletteColor color) => new Color(color.R, color.G, color.B);
    }
}
