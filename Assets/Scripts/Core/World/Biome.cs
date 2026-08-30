using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // MVP ships Jungle and Volcanic only — docs/DECISIONS.md D6. Desert and the biomes listed
    // in CLAUDE.md section 7 are content updates; the point of this enum plus BiomeDefinition
    // is that adding one is a data change, not a code change (section 50).
    public enum BiomeType
    {
        Jungle,
        Volcanic,
    }

    public sealed class BiomeDefinition
    {
        public BiomeType Type { get; }
        public string DisplayName { get; }
        public BiomePalette Palette { get; }

        public BiomeDefinition(BiomeType type, string displayName, BiomePalette palette)
        {
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));

            Type = type;
            DisplayName = displayName;
            Palette = palette ?? throw new ArgumentNullException(nameof(palette));
        }
    }

    public static class BiomeLibrary
    {
        // Warm natural daylight over dense green (section 6, biome 1).
        public static BiomeDefinition Jungle { get; } = new BiomeDefinition(
            BiomeType.Jungle,
            "Prehistoric Jungle",
            new BiomePalette(
                sky: new PaletteColor(0.36f, 0.45f, 0.30f),
                ground: new PaletteColor(0.30f, 0.38f, 0.22f),
                scenery: new PaletteColor(0.22f, 0.30f, 0.16f),
                groundObstacle: new PaletteColor(0.65f, 0.25f, 0.15f),
                overheadObstacle: new PaletteColor(0.35f, 0.30f, 0.55f)));

        // Ash, ember light and burned ground (section 6, biome 3). Dramatic but still
        // mobile-friendly — this is a palette shift, not extra lights or post-processing.
        public static BiomeDefinition Volcanic { get; } = new BiomeDefinition(
            BiomeType.Volcanic,
            "Volcanic Land",
            new BiomePalette(
                sky: new PaletteColor(0.28f, 0.13f, 0.10f),
                ground: new PaletteColor(0.20f, 0.14f, 0.12f),
                scenery: new PaletteColor(0.14f, 0.09f, 0.08f),
                groundObstacle: new PaletteColor(0.85f, 0.35f, 0.12f),
                overheadObstacle: new PaletteColor(0.55f, 0.20f, 0.35f)));

        public static IReadOnlyList<BiomeDefinition> All { get; } = new[] { Jungle, Volcanic };

        public static BiomeDefinition Get(BiomeType type)
        {
            foreach (var biome in All)
                if (biome.Type == type) return biome;

            throw new ArgumentOutOfRangeException(nameof(type), $"No definition registered for {type}.");
        }
    }
}
