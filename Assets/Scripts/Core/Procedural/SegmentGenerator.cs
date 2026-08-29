using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // The procedural segment system from CLAUDE.md section 15. Determinism (same seed ->
    // same run, on every runtime) comes from SeededRandom, never System.Random — see its own
    // comment for why.
    public sealed class SegmentGenerator
    {
        private readonly RunGenerationConfig _config;
        private readonly IReadOnlyList<SegmentTemplate> _templates;

        public SegmentGenerator(RunGenerationConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _templates = SegmentTemplateLibrary.CreateDefaultTemplates(config);
        }

        public RunGenerationResult GenerateRun(int seed, float targetLengthMeters)
        {
            if (targetLengthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(targetLengthMeters));

            var random = new SeededRandom(seed);
            var segments = new List<GeneratedSegment>();
            var obstacles = new List<ObstacleSpawn>();
            var coins = new List<CoinSpawn>();

            float cumulativeDistance = 0f;
            float elapsedSeconds = 0f;

            while (cumulativeDistance < targetLengthMeters)
            {
                var tier = _config.Difficulty.GetTierWindow(elapsedSeconds);
                var template = PickTemplate(random, tier.Tier);

                foreach (var (offset, width, action) in template.Obstacles)
                    obstacles.Add(new ObstacleSpawn(cumulativeDistance + offset, width, action));
                foreach (var offset in template.CoinOffsetsMeters)
                    coins.Add(new CoinSpawn(cumulativeDistance + offset));

                segments.Add(new GeneratedSegment(template.Type, cumulativeDistance, template.LengthMeters, tier.Tier));

                float speed = _config.BaseRunSpeedMetersPerSecond * tier.RunSpeedMultiplier;
                elapsedSeconds += template.LengthMeters / speed;
                cumulativeDistance += template.LengthMeters;
            }

            return new RunGenerationResult(seed, cumulativeDistance, segments, obstacles, coins);
        }

        private SegmentTemplate PickTemplate(SeededRandom random, DifficultyTier tier)
        {
            var weighted = new List<(SegmentTemplate, double)>(_templates.Count);
            foreach (var template in _templates)
                weighted.Add((template, SegmentWeights.GetWeight(template.Type, tier)));
            return random.WeightedPick(weighted);
        }
    }
}
