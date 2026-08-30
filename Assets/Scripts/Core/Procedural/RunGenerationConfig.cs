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

        // What the player can physically do. Generation depends on it in two places: obstacle
        // spacing has to respect reaction time at speed, and coin heights have to stay within
        // reach of a jump. Holding it here keeps both answers derived from one source rather
        // than duplicated as constants.
        public PlayerMotorConfig Player { get; }

        public float BaseRunSpeedMetersPerSecond { get; }
        public float MinReactionTimeSeconds { get; }
        public float ObstacleWidthMeters { get; }
        public float SafetyMarginMeters { get; }
        public float CoinRadiusMeters { get; }

        public RunGenerationConfig(
            DifficultyConfig difficulty,
            PlayerMotorConfig player,
            float baseRunSpeedMetersPerSecond,
            float minReactionTimeSeconds,
            float obstacleWidthMeters,
            float safetyMarginMeters,
            float coinRadiusMeters)
        {
            Difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
            Player = player ?? throw new ArgumentNullException(nameof(player));
            if (baseRunSpeedMetersPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(baseRunSpeedMetersPerSecond));
            if (minReactionTimeSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(minReactionTimeSeconds));
            if (obstacleWidthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(obstacleWidthMeters));
            if (safetyMarginMeters < 0) throw new ArgumentOutOfRangeException(nameof(safetyMarginMeters));
            if (coinRadiusMeters <= 0) throw new ArgumentOutOfRangeException(nameof(coinRadiusMeters));

            BaseRunSpeedMetersPerSecond = baseRunSpeedMetersPerSecond;
            MinReactionTimeSeconds = minReactionTimeSeconds;
            ObstacleWidthMeters = obstacleWidthMeters;
            SafetyMarginMeters = safetyMarginMeters;
            CoinRadiusMeters = coinRadiusMeters;
        }

        // The highest a coin may sit and still be collectible. Deliberately below the absolute
        // ceiling (apex + full standing height, i.e. grazing it with the top of the head at the
        // exact peak of a perfect jump): a coin that requires frame-perfect timing reads as
        // broken rather than skilful, so half the player's height is held back as margin.
        public float MaxCoinHeightMeters =>
            Player.JumpApexMeters + Player.StandingHeightMeters * 0.5f;

        public float MaxRunSpeedMetersPerSecond => BaseRunSpeedMetersPerSecond * Difficulty.MaxRunSpeedMultiplier;

        // The hard floor every generated run must respect, at every tier, regardless of how
        // aggressively segment weighting escalates — CLAUDE.md section 48: "prevent unavoidable
        // deaths", "minimum reaction time". Derived from the *fastest* speed the run ever
        // reaches, so it stays safe even in the worst case.
        public float MinObstacleGapMeters => MinReactionTimeSeconds * MaxRunSpeedMetersPerSecond + SafetyMarginMeters;

        public static RunGenerationConfig CreateDefault() => new RunGenerationConfig(
            difficulty: DifficultyConfig.CreateDefault(),
            player: PlayerMotorConfig.CreateDefault(),
            baseRunSpeedMetersPerSecond: 8f,
            minReactionTimeSeconds: 0.5f,
            obstacleWidthMeters: 1.2f,
            safetyMarginMeters: 1f,
            coinRadiusMeters: 0.35f);
    }
}
