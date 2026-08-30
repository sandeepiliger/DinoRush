using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    public sealed class SegmentTemplate
    {
        public SegmentType Type { get; }
        public float LengthMeters { get; }
        public IReadOnlyList<(float OffsetMeters, float WidthMeters, PlayerAction Action)> Obstacles { get; }
        public IReadOnlyList<(float OffsetMeters, float HeightMeters)> Coins { get; }

        public SegmentTemplate(
            SegmentType type,
            float lengthMeters,
            IReadOnlyList<(float, float, PlayerAction)> obstacles,
            IReadOnlyList<(float, float)> coins)
        {
            if (lengthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(lengthMeters));

            foreach (var (offset, width, _) in obstacles)
            {
                if (offset < 0 || offset + width > lengthMeters)
                    throw new ArgumentException($"Obstacle at {offset}m (width {width}m) does not fit within a {lengthMeters}m segment.", nameof(obstacles));
            }
            foreach (var (offset, height) in coins)
            {
                if (offset < 0 || offset > lengthMeters)
                    throw new ArgumentException($"Coin at {offset}m falls outside a {lengthMeters}m segment.", nameof(coins));
                if (height < 0)
                    throw new ArgumentException($"Coin at {offset}m has a negative height.", nameof(coins));
            }

            Type = type;
            LengthMeters = lengthMeters;
            Obstacles = obstacles;
            Coins = coins;
        }
    }

    // Builds the default template library. Every obstacle-bearing template reserves exactly
    // one MinObstacleGapMeters of clear space before its first obstacle and after its last —
    // which, by construction, guarantees the run-wide minimum-gap invariant across ANY
    // concatenation of templates (two obstacle-bearing segments placed back to back still
    // clear 2x the floor: the trailing buffer of one plus the leading buffer of the next).
    // RunValidator re-checks this independently — CLAUDE.md section 48 asks for a validator
    // regardless of how confident the generator's construction is.
    //
    // Coin-bearing templates (Safe, CoinPattern) never carry obstacles, and obstacle-bearing
    // templates never carry coins — this sidesteps "valid coin paths" entirely by construction
    // rather than by computing per-coin clearance from every obstacle at generation time.
    public static class SegmentTemplateLibrary
    {
        public static IReadOnlyList<SegmentTemplate> CreateDefaultTemplates(RunGenerationConfig config)
        {
            float gap = config.MinObstacleGapMeters;
            float width = config.ObstacleWidthMeters;
            float runHeight = config.Player.StandingHeightMeters * 0.4f;

            return new[]
            {
                // Running-height coins: collected simply by not dying.
                new SegmentTemplate(SegmentType.Safe, gap * 3f,
                    Array.Empty<(float, float, PlayerAction)>(),
                    new[] { (gap * 0.5f, runHeight), (gap * 1.5f, runHeight), (gap * 2.5f, runHeight) }),

                BuildObstacleTemplate(SegmentType.SmallObstacle, gap, width, PlayerAction.Jump),

                BuildObstacleTemplate(SegmentType.JumpChallenge, gap, width, PlayerAction.Jump, PlayerAction.Jump),

                // An arc peaking near the jump apex — the one segment that rewards jumping when
                // nothing is forcing you to, so a safe stretch still asks something of the player.
                BuildCoinArcTemplate(config, gap),

                BuildObstacleTemplate(SegmentType.Enemy, gap, width, PlayerAction.Duck),

                BuildObstacleTemplate(SegmentType.MixedObstacle, gap, width, PlayerAction.Jump, PlayerAction.Duck),

                BuildObstacleTemplate(SegmentType.HighDifficulty, gap, width, PlayerAction.Jump, PlayerAction.Duck, PlayerAction.Jump),
            };
        }

        private static SegmentTemplate BuildCoinArcTemplate(RunGenerationConfig config, float gap)
        {
            float length = gap * 3f;
            float peak = config.MaxCoinHeightMeters;
            float baseHeight = config.Player.StandingHeightMeters * 0.4f;

            // Five coins tracing a shallow parabola from running height up to the peak and back.
            var coins = new List<(float, float)>(5);
            const int count = 5;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);          // 0..1 across the segment
                float arc = 1f - (2f * t - 1f) * (2f * t - 1f); // 0 at the ends, 1 in the middle
                coins.Add((length * (0.15f + 0.7f * t), baseHeight + (peak - baseHeight) * arc));
            }

            return new SegmentTemplate(SegmentType.CoinPattern, length,
                Array.Empty<(float, float, PlayerAction)>(), coins);
        }

        // Lays out a chain of obstacles each separated by exactly `gap`, with a leading and
        // trailing buffer of `gap` too — see the class-level comment for why this is safe.
        private static SegmentTemplate BuildObstacleTemplate(SegmentType type, float gap, float width, params PlayerAction[] actions)
        {
            var obstacles = new List<(float, float, PlayerAction)>(actions.Length);
            float cursor = gap;
            foreach (var action in actions)
            {
                obstacles.Add((cursor, width, action));
                cursor += width + gap;
            }
            float length = cursor;
            return new SegmentTemplate(type, length, obstacles, Array.Empty<(float, float)>());
        }
    }

    internal static class SegmentWeights
    {
        // Rows: SegmentType. Columns: DifficultyTier (Calm..Extinction), ascending. Higher
        // tiers shift weight toward harder types per CLAUDE.md section 16; the safety floor
        // itself (RunGenerationConfig.MinObstacleGapMeters) never changes by tier.
        private static readonly Dictionary<SegmentType, double[]> Table = new Dictionary<SegmentType, double[]>
        {
            [SegmentType.Safe] = new[] { 30.0, 15.0, 8.0, 4.0, 2.0 },
            [SegmentType.CoinPattern] = new[] { 20.0, 15.0, 10.0, 6.0, 4.0 },
            [SegmentType.SmallObstacle] = new[] { 30.0, 25.0, 18.0, 12.0, 8.0 },
            [SegmentType.JumpChallenge] = new[] { 10.0, 18.0, 20.0, 18.0, 14.0 },
            [SegmentType.Enemy] = new[] { 8.0, 15.0, 18.0, 18.0, 16.0 },
            [SegmentType.MixedObstacle] = new[] { 2.0, 10.0, 16.0, 20.0, 20.0 },
            [SegmentType.HighDifficulty] = new[] { 0.0, 2.0, 10.0, 22.0, 36.0 },
        };

        public static double GetWeight(SegmentType type, DifficultyTier tier) => Table[type][(int)tier];
    }
}
