using System;

namespace DinoRush.Core
{
    // What the world looks like, and how close it is to collapse, at a given moment in a run.
    public readonly struct WorldState
    {
        public BiomeDefinition Biome { get; }
        public BiomePalette Palette { get; }
        public bool IsExtinctionActive { get; }

        // 0 while the world is stable, ramping to 1 as extinction takes hold. Drives intensity
        // of whatever the Runtime layer wants to escalate — shake, tint, particle density —
        // without Core needing to know what any of those are.
        public float ExtinctionIntensity { get; }

        public WorldState(BiomeDefinition biome, BiomePalette palette, bool isExtinctionActive, float extinctionIntensity)
        {
            Biome = biome;
            Palette = palette;
            IsExtinctionActive = isExtinctionActive;
            ExtinctionIntensity = extinctionIntensity;
        }
    }

    // The escalation from CLAUDE.md section 5 — the game's stated visual and emotional
    // identity — expressed as a pure function of elapsed time (docs/DECISIONS.md D4).
    //
    // MVP runs Jungle into a Volcanic extinction climax (D6). The transition is a blend rather
    // than a cut: section 63 wants the world to feel like it is collapsing around the player,
    // and a hard swap reads as a level change instead.
    public sealed class BiomeSchedule
    {
        // Seconds over which one biome dissolves into the next.
        private const float TransitionSeconds = 8f;

        private readonly DifficultyConfig _difficulty;

        public BiomeSchedule(DifficultyConfig difficulty)
        {
            _difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
        }

        // Volcanic takes over as the world turns hostile, which is where section 5 puts the
        // volcano; extinction proper begins one tier later.
        public float VolcanicStartSeconds => StartTimeOf(DifficultyTier.PreExtinction);
        public float ExtinctionStartSeconds => StartTimeOf(DifficultyTier.Extinction);

        public BiomeType GetBiome(float elapsedSeconds)
        {
            return elapsedSeconds >= VolcanicStartSeconds ? BiomeType.Volcanic : BiomeType.Jungle;
        }

        public WorldState GetWorldState(float elapsedSeconds)
        {
            if (elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

            var biome = BiomeLibrary.Get(GetBiome(elapsedSeconds));

            // Blend the palette across the boundary so the world visibly turns rather than cuts.
            float blend = Progress(elapsedSeconds, VolcanicStartSeconds - TransitionSeconds, TransitionSeconds);
            var palette = BiomePalette.Lerp(BiomeLibrary.Jungle.Palette, BiomeLibrary.Volcanic.Palette, blend);

            bool extinction = elapsedSeconds >= ExtinctionStartSeconds;
            // Ramps in over the same window length so extinction arrives as a build, not a flag flip.
            float intensity = Progress(elapsedSeconds, ExtinctionStartSeconds, TransitionSeconds);

            return new WorldState(biome, palette, extinction, intensity);
        }

        private float StartTimeOf(DifficultyTier tier)
        {
            foreach (var window in _difficulty.Tiers)
                if (window.Tier == tier) return window.StartTimeSeconds;

            throw new InvalidOperationException($"The difficulty config defines no {tier} tier.");
        }

        // Fraction of the way through a window starting at `start` and lasting `duration`,
        // clamped to 0..1.
        private static float Progress(float value, float start, float duration)
        {
            if (duration <= 0f) return value >= start ? 1f : 0f;
            float t = (value - start) / duration;
            return t < 0f ? 0f : (t > 1f ? 1f : t);
        }
    }
}
