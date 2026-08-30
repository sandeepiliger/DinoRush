using System;

namespace DinoRush.Core
{
    // Centralizes every tunable number the procedural generator and its validator agree on,
    // so the safety floor (CLAUDE.md section 48) is derived once, not hardcoded in multiple
    // places. See docs/DECISIONS.md D9 for why this lives in Core rather than a Unity
    // ScriptableObject at this stage.
    public sealed class RunGenerationConfig
    {
        public DifficultyConfig Difficulty { get; }
        public float BaseRunSpeedMetersPerSecond { get; }
        public float MinReactionTimeSeconds { get; }
        public float ObstacleWidthMeters { get; }
        public float SafetyMarginMeters { get; }

        public RunGenerationConfig(
            DifficultyConfig difficulty,
            float baseRunSpeedMetersPerSecond,
            float minReactionTimeSeconds,
            float obstacleWidthMeters,
            float safetyMarginMeters)
        {
            Difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
            if (baseRunSpeedMetersPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(baseRunSpeedMetersPerSecond));
            if (minReactionTimeSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(minReactionTimeSeconds));
            if (obstacleWidthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(obstacleWidthMeters));
            if (safetyMarginMeters < 0) throw new ArgumentOutOfRangeException(nameof(safetyMarginMeters));

            BaseRunSpeedMetersPerSecond = baseRunSpeedMetersPerSecond;
            MinReactionTimeSeconds = minReactionTimeSeconds;
            ObstacleWidthMeters = obstacleWidthMeters;
            SafetyMarginMeters = safetyMarginMeters;
        }

        public float MaxRunSpeedMetersPerSecond => BaseRunSpeedMetersPerSecond * Difficulty.MaxRunSpeedMultiplier;

        // The hard floor every generated run must respect, at every tier, regardless of how
        // aggressively segment weighting escalates — CLAUDE.md section 48: "prevent unavoidable
        // deaths", "minimum reaction time". Derived from the *fastest* speed the run ever
        // reaches, so it stays safe even in the worst case.
        public float MinObstacleGapMeters => MinReactionTimeSeconds * MaxRunSpeedMetersPerSecond + SafetyMarginMeters;

        public static RunGenerationConfig CreateDefault() => new RunGenerationConfig(
            difficulty: DifficultyConfig.CreateDefault(),
            baseRunSpeedMetersPerSecond: 8f,
            minReactionTimeSeconds: 0.5f,
            obstacleWidthMeters: 1.2f,
            safetyMarginMeters: 1f);
    }
}
