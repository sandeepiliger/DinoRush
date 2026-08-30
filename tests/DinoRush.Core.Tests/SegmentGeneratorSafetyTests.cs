using System.Collections.Generic;
using System.Linq;
using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    // CLAUDE.md section 48: "Create automated procedural-generation tests. Generate thousands
    // of segments in editor tests and verify validity." This is that suite — run entirely
    // outside Unity (docs/DECISIONS.md D9), so it costs nothing to run on every push.
    [TestFixture]
    public class SegmentGeneratorSafetyTests
    {
        private const int SeedCount = 2000;
        private const float RunLengthMeters = 3000f; // long enough to pass through every tier

        [Test]
        public void GeneratedRuns_AreAlwaysValid_AcrossThousandsOfSeeds()
        {
            var config = RunGenerationConfig.CreateDefault();
            var generator = new SegmentGenerator(config);
            var validator = new RunValidator(config);

            var failures = new List<string>();
            int totalObstaclesChecked = 0;

            for (int seed = 0; seed < SeedCount; seed++)
            {
                var run = generator.GenerateRun(seed, RunLengthMeters);
                totalObstaclesChecked += run.Obstacles.Count;

                var result = validator.Validate(run);
                if (!result.IsValid)
                {
                    failures.Add($"seed {seed}: {string.Join("; ", result.Violations)}");
                }
            }

            // A real assertion, not a smoke test: prove the suite actually exercised the
            // safety-critical path across every seed before trusting the "no failures" result.
            Assert.That(totalObstaclesChecked, Is.GreaterThan(SeedCount * 10),
                "Expected a substantial number of obstacles across all generated runs — a near-zero count would mean this test isn't actually exercising the generator.");

            Assert.That(failures, Is.Empty,
                $"{failures.Count} of {SeedCount} generated runs violated procedural-generation safety rules:\n" +
                string.Join("\n", failures.Take(10)));
        }

        [Test]
        public void GeneratedRuns_AreDeterministic_ForTheSameSeed()
        {
            var config = RunGenerationConfig.CreateDefault();
            var generator = new SegmentGenerator(config);

            var first = generator.GenerateRun(12345, RunLengthMeters);
            var second = generator.GenerateRun(12345, RunLengthMeters);

            Assert.That(second.Obstacles.Count, Is.EqualTo(first.Obstacles.Count));
            for (int i = 0; i < first.Obstacles.Count; i++)
            {
                Assert.That(second.Obstacles[i].DistanceMeters, Is.EqualTo(first.Obstacles[i].DistanceMeters));
                Assert.That(second.Obstacles[i].RequiredAction, Is.EqualTo(first.Obstacles[i].RequiredAction));
            }
        }

        [Test]
        public void ObstacleDensity_NeverMeaningfullyDecreases_AsDifficultyTierRises()
        {
            // A statistical property, not a per-run guarantee (segment selection is weighted,
            // not deterministic by tier) — so this samples many runs and checks the aggregate
            // trend, which is what "valid difficulty progression" (section 48) means for a
            // weighted-random generator. A 10% tolerance absorbs natural sampling noise while
            // still catching a real regression.
            var config = RunGenerationConfig.CreateDefault();
            var generator = new SegmentGenerator(config);

            var obstaclesPerTier = new Dictionary<DifficultyTier, int>();
            var metersPerTier = new Dictionary<DifficultyTier, float>();
            foreach (DifficultyTier tier in System.Enum.GetValues(typeof(DifficultyTier)))
            {
                obstaclesPerTier[tier] = 0;
                metersPerTier[tier] = 0f;
            }

            for (int seed = 0; seed < 500; seed++)
            {
                var run = generator.GenerateRun(seed, RunLengthMeters);
                foreach (var segment in run.Segments)
                    metersPerTier[segment.TierAtGeneration] += segment.LengthMeters;

                foreach (var obstacle in run.Obstacles)
                {
                    var owningSegment = run.Segments.Last(s =>
                        obstacle.DistanceMeters >= s.StartDistanceMeters &&
                        obstacle.DistanceMeters < s.StartDistanceMeters + s.LengthMeters);
                    obstaclesPerTier[owningSegment.TierAtGeneration]++;
                }
            }

            var tiersInOrder = new[]
            {
                DifficultyTier.Calm, DifficultyTier.Rising, DifficultyTier.Hazard,
                DifficultyTier.PreExtinction, DifficultyTier.Extinction,
            };

            double previousDensity = 0;
            foreach (var tier in tiersInOrder)
            {
                if (metersPerTier[tier] <= 0) continue;
                double density = obstaclesPerTier[tier] / (double)metersPerTier[tier];
                Assert.That(density, Is.GreaterThanOrEqualTo(previousDensity * 0.9),
                    $"Obstacle density dropped from {previousDensity:F4}/m to {density:F4}/m entering {tier} — difficulty progression should not meaningfully reverse.");
                previousDensity = density;
            }
        }
    }
}
