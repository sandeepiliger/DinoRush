using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    public sealed class RunGenerationResult
    {
        public int Seed { get; }
        public float TotalLengthMeters { get; }
        public IReadOnlyList<GeneratedSegment> Segments { get; }
        public IReadOnlyList<ObstacleSpawn> Obstacles { get; }
        public IReadOnlyList<CoinSpawn> Coins { get; }

        public RunGenerationResult(
            int seed,
            float totalLengthMeters,
            IReadOnlyList<GeneratedSegment> segments,
            IReadOnlyList<ObstacleSpawn> obstacles,
            IReadOnlyList<CoinSpawn> coins)
        {
            Seed = seed;
            TotalLengthMeters = totalLengthMeters;
            Segments = segments ?? throw new ArgumentNullException(nameof(segments));
            Obstacles = obstacles ?? throw new ArgumentNullException(nameof(obstacles));
            Coins = coins ?? throw new ArgumentNullException(nameof(coins));
        }
    }
}
