namespace DinoRush.Core
{
    public enum PlayerAction
    {
        Jump,
        Duck,
    }

    public readonly struct ObstacleSpawn
    {
        public float DistanceMeters { get; }
        public float WidthMeters { get; }
        public PlayerAction RequiredAction { get; }

        public ObstacleSpawn(float distanceMeters, float widthMeters, PlayerAction requiredAction)
        {
            DistanceMeters = distanceMeters;
            WidthMeters = widthMeters;
            RequiredAction = requiredAction;
        }
    }

    public readonly struct CoinSpawn
    {
        public float DistanceMeters { get; }

        // Height off the ground. Coins are not all at running height: arcs that peak near the
        // jump apex reward jumping for its own sake, which is what makes an empty stretch of
        // track worth playing instead of just waiting. RunValidator proves every coin is
        // actually reachable given the player's jump.
        public float HeightMeters { get; }

        public CoinSpawn(float distanceMeters, float heightMeters)
        {
            DistanceMeters = distanceMeters;
            HeightMeters = heightMeters;
        }
    }

    // Matches the example segment categories in CLAUDE.md section 15 (segments A-G).
    public enum SegmentType
    {
        Safe,
        SmallObstacle,
        JumpChallenge,
        CoinPattern,
        Enemy,
        MixedObstacle,
        HighDifficulty,
    }

    public sealed class GeneratedSegment
    {
        public SegmentType Type { get; }
        public float StartDistanceMeters { get; }
        public float LengthMeters { get; }
        public DifficultyTier TierAtGeneration { get; }

        public GeneratedSegment(SegmentType type, float startDistanceMeters, float lengthMeters, DifficultyTier tierAtGeneration)
        {
            Type = type;
            StartDistanceMeters = startDistanceMeters;
            LengthMeters = lengthMeters;
            TierAtGeneration = tierAtGeneration;
        }
    }
}
