using System;

namespace DinoRush.Core
{
    // A plain RGB triple. Core cannot reference UnityEngine.Color (docs/DECISIONS.md D9) but
    // biome identity is fundamentally about colour, so the palette lives here as data and the
    // Runtime layer converts it at the boundary.
    public readonly struct PaletteColor
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }

        public PaletteColor(float r, float g, float b)
        {
            R = r; G = g; B = b;
        }

        public static PaletteColor Lerp(PaletteColor from, PaletteColor to, float t)
        {
            if (t <= 0f) return from;
            if (t >= 1f) return to;
            return new PaletteColor(
                from.R + (to.R - from.R) * t,
                from.G + (to.G - from.G) * t,
                from.B + (to.B - from.B) * t);
        }

        public override string ToString() => $"({R:F2}, {G:F2}, {B:F2})";
    }

    // The full set of colours that define how a biome reads on screen.
    public sealed class BiomePalette
    {
        public PaletteColor Sky { get; }
        public PaletteColor Ground { get; }
        public PaletteColor Scenery { get; }
        public PaletteColor GroundObstacle { get; }
        public PaletteColor OverheadObstacle { get; }

        public BiomePalette(
            PaletteColor sky, PaletteColor ground, PaletteColor scenery,
            PaletteColor groundObstacle, PaletteColor overheadObstacle)
        {
            Sky = sky;
            Ground = ground;
            Scenery = scenery;
            GroundObstacle = groundObstacle;
            OverheadObstacle = overheadObstacle;
        }

        public static BiomePalette Lerp(BiomePalette from, BiomePalette to, float t)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));

            return new BiomePalette(
                PaletteColor.Lerp(from.Sky, to.Sky, t),
                PaletteColor.Lerp(from.Ground, to.Ground, t),
                PaletteColor.Lerp(from.Scenery, to.Scenery, t),
                PaletteColor.Lerp(from.GroundObstacle, to.GroundObstacle, t),
                PaletteColor.Lerp(from.OverheadObstacle, to.OverheadObstacle, t));
        }
    }
}
