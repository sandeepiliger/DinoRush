using System;
using System.Collections.Generic;
using System.Linq;

namespace DinoRush.Core
{
    // The single centralized source of difficulty-by-time values — CLAUDE.md section 16:
    // "Create a centralized DifficultyConfig. Never hardcode difficulty values throughout
    // unrelated scripts."
    public sealed class DifficultyConfig
    {
        public IReadOnlyList<DifficultyTierWindow> Tiers { get; }

        public DifficultyConfig(IReadOnlyList<DifficultyTierWindow> tiers)
        {
            if (tiers == null || tiers.Count == 0)
                throw new ArgumentException("At least one difficulty tier is required.", nameof(tiers));
            if (tiers[0].StartTimeSeconds != 0)
                throw new ArgumentException("The first tier must start at 0 seconds.", nameof(tiers));
            for (int i = 1; i < tiers.Count; i++)
            {
                if (tiers[i].StartTimeSeconds <= tiers[i - 1].StartTimeSeconds)
                    throw new ArgumentException("Tiers must be strictly ascending by start time.", nameof(tiers));
            }

            Tiers = tiers;
        }

        public DifficultyTierWindow GetTierWindow(float elapsedSeconds)
        {
            if (elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

            var active = Tiers[0];
            foreach (var tier in Tiers)
            {
                if (tier.StartTimeSeconds > elapsedSeconds) break;
                active = tier;
            }
            return active;
        }

        public float MaxRunSpeedMultiplier => Tiers.Max(t => t.RunSpeedMultiplier);

        // Matches the timeline and escalation intent of CLAUDE.md section 5.
        public static DifficultyConfig CreateDefault() => new DifficultyConfig(new[]
        {
            new DifficultyTierWindow(DifficultyTier.Calm, 0f, 1.0f),
            new DifficultyTierWindow(DifficultyTier.Rising, 30f, 1.15f),
            new DifficultyTierWindow(DifficultyTier.Hazard, 60f, 1.3f),
            new DifficultyTierWindow(DifficultyTier.PreExtinction, 90f, 1.45f),
            new DifficultyTierWindow(DifficultyTier.Extinction, 120f, 1.6f),
        });
    }
}
