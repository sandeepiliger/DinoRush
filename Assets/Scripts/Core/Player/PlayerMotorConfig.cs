using System;

namespace DinoRush.Core
{
    // Every number that decides whether a run is physically survivable. These are not
    // arbitrary: PlayerMotorConfigSafetyTests proves each constraint below actually holds
    // against RunGenerationConfig's spacing floor, so a careless tweak here fails CI rather
    // than shipping an unclearable obstacle.
    public sealed class PlayerMotorConfig
    {
        public float JumpVelocityMetersPerSecond { get; }
        public float GravityMetersPerSecondSquared { get; }
        public float StandingHeightMeters { get; }
        public float DuckingHeightMeters { get; }
        public float DuckDurationSeconds { get; }
        public float PlayerHalfWidthMeters { get; }

        // Vertical extents of the two obstacle kinds. A Jump obstacle sits on the ground and
        // must be cleared by getting the player's feet above it; a Duck obstacle hangs
        // overhead and must be passed under by shrinking the player's silhouette.
        public float JumpObstacleHeightMeters { get; }
        public float DuckObstacleBottomMeters { get; }
        public float DuckObstacleTopMeters { get; }

        public PlayerMotorConfig(
            float jumpVelocityMetersPerSecond,
            float gravityMetersPerSecondSquared,
            float standingHeightMeters,
            float duckingHeightMeters,
            float duckDurationSeconds,
            float playerHalfWidthMeters,
            float jumpObstacleHeightMeters,
            float duckObstacleBottomMeters,
            float duckObstacleTopMeters)
        {
            if (jumpVelocityMetersPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(jumpVelocityMetersPerSecond));
            if (gravityMetersPerSecondSquared <= 0) throw new ArgumentOutOfRangeException(nameof(gravityMetersPerSecondSquared));
            if (duckingHeightMeters <= 0 || duckingHeightMeters >= standingHeightMeters)
                throw new ArgumentOutOfRangeException(nameof(duckingHeightMeters), "Ducking must be shorter than standing.");
            if (duckDurationSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(duckDurationSeconds));
            if (playerHalfWidthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(playerHalfWidthMeters));
            if (duckObstacleTopMeters <= duckObstacleBottomMeters)
                throw new ArgumentOutOfRangeException(nameof(duckObstacleTopMeters));

            JumpVelocityMetersPerSecond = jumpVelocityMetersPerSecond;
            GravityMetersPerSecondSquared = gravityMetersPerSecondSquared;
            StandingHeightMeters = standingHeightMeters;
            DuckingHeightMeters = duckingHeightMeters;
            DuckDurationSeconds = duckDurationSeconds;
            PlayerHalfWidthMeters = playerHalfWidthMeters;
            JumpObstacleHeightMeters = jumpObstacleHeightMeters;
            DuckObstacleBottomMeters = duckObstacleBottomMeters;
            DuckObstacleTopMeters = duckObstacleTopMeters;
        }

        // Peak height of a jump: v^2 / 2g.
        public float JumpApexMeters =>
            (JumpVelocityMetersPerSecond * JumpVelocityMetersPerSecond) / (2f * GravityMetersPerSecondSquared);

        // Total time from leaving the ground to landing: 2v / g.
        public float JumpAirtimeSeconds => (2f * JumpVelocityMetersPerSecond) / GravityMetersPerSecondSquared;

        // Tuned so that, at the fastest speed the game ever reaches, a jump both clears a
        // ground obstacle AND lands before the next obstacle can arrive — otherwise a jump
        // over one obstacle could strand the player airborne into a duck obstacle, which is
        // unavoidable-by-construction and exactly what CLAUDE.md section 48 forbids.
        public static PlayerMotorConfig CreateDefault() => new PlayerMotorConfig(
            jumpVelocityMetersPerSecond: 10f,
            gravityMetersPerSecondSquared: 40f,
            standingHeightMeters: 1.8f,
            duckingHeightMeters: 0.9f,
            duckDurationSeconds: 0.6f,
            playerHalfWidthMeters: 0.4f,
            jumpObstacleHeightMeters: 0.8f,
            duckObstacleBottomMeters: 1.1f,
            duckObstacleTopMeters: 3f);
    }
}
