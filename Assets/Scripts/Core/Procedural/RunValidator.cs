using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // Independent check of everything CLAUDE.md section 48 requires. The generator is built
    // to satisfy these by construction (see SegmentTemplateLibrary's class comment), but the
    // spec explicitly asks for a validator regardless — this is that validator, and the
    // procedural-safety tests run it across thousands of generated runs.
    public sealed class RunValidator
    {
        private readonly RunGenerationConfig _config;

        public RunValidator(RunGenerationConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public ValidationResult Validate(RunGenerationResult result)
        {
            var violations = new List<string>();
            ValidateSegmentContiguity(result, violations);
            ValidateObstacleSpacing(result, violations);
            ValidateCoinPlacement(result, violations);
            return new ValidationResult(violations);
        }

        private static void ValidateSegmentContiguity(RunGenerationResult result, List<string> violations)
        {
            float expectedStart = 0f;
            foreach (var segment in result.Segments)
            {
                if (Math.Abs(segment.StartDistanceMeters - expectedStart) > 0.01f)
                {
                    violations.Add(
                        $"Segment '{segment.Type}' starts at {segment.StartDistanceMeters}m but the previous segment ended at {expectedStart}m (broken segment transition).");
                }
                expectedStart = segment.StartDistanceMeters + segment.LengthMeters;
            }

            if (result.Segments.Count > 0 && Math.Abs(expectedStart - result.TotalLengthMeters) > 0.01f)
            {
                violations.Add(
                    $"Segments end at {expectedStart}m but the run reports a total length of {result.TotalLengthMeters}m.");
            }
        }

        // The generator places obstacles at *exactly* the minimum gap by construction (see
        // SegmentTemplateLibrary), and cumulative float addition across hundreds of meters of
        // segment lengths drifts by a fraction of a millimeter — enough for an exact-boundary
        // gap to land a hair under the floor (e.g. 7.399999 vs 7.4). This epsilon absorbs that
        // float noise without weakening the actual safety floor by any gameplay-relevant amount.
        private const float FloatTolerance = 0.01f;

        private void ValidateObstacleSpacing(RunGenerationResult result, List<string> violations)
        {
            float minGap = _config.MinObstacleGapMeters;
            float previousEnd = float.NegativeInfinity;

            foreach (var obstacle in result.Obstacles)
            {
                if (obstacle.DistanceMeters < 0 || obstacle.DistanceMeters > result.TotalLengthMeters)
                {
                    violations.Add($"Obstacle at {obstacle.DistanceMeters}m falls outside the run's [0, {result.TotalLengthMeters}] range.");
                }

                if (float.IsNegativeInfinity(previousEnd))
                {
                    if (obstacle.DistanceMeters < minGap - FloatTolerance)
                    {
                        violations.Add(
                            $"The first obstacle at {obstacle.DistanceMeters}m is closer than the minimum reaction gap ({minGap:F2}m) to the player's start position.");
                    }
                }
                else
                {
                    float actualGap = obstacle.DistanceMeters - previousEnd;
                    if (actualGap < minGap - FloatTolerance)
                    {
                        violations.Add(
                            $"Obstacle at {obstacle.DistanceMeters}m is only {actualGap:F2}m past the previous obstacle (ends at {previousEnd:F2}m); needs at least {minGap:F2}m (minimum reaction time / no impossible combination).");
                    }
                }

                previousEnd = obstacle.DistanceMeters + obstacle.WidthMeters;
            }
        }

        private void ValidateCoinPlacement(RunGenerationResult result, List<string> violations)
        {
            float buffer = _config.MinObstacleGapMeters;
            float maxHeight = _config.MaxCoinHeightMeters;

            foreach (var coin in result.Coins)
            {
                // "Valid coin paths" (section 48) means two things: a coin must not sit where
                // dodging an obstacle takes the player, and it must be physically reachable.
                // An uncollectible coin isn't a fair challenge, it's a bug the player reads as
                // the game cheating.
                if (coin.HeightMeters > maxHeight)
                {
                    violations.Add(
                        $"Coin at {coin.DistanceMeters}m sits at {coin.HeightMeters:F2}m, above the reachable ceiling of {maxHeight:F2}m (jump apex {_config.Player.JumpApexMeters:F2}m).");
                }

                foreach (var obstacle in result.Obstacles)
                {
                    float zoneStart = obstacle.DistanceMeters - buffer;
                    float zoneEnd = obstacle.DistanceMeters + obstacle.WidthMeters + buffer;
                    if (coin.DistanceMeters >= zoneStart && coin.DistanceMeters <= zoneEnd)
                    {
                        violations.Add(
                            $"Coin at {coin.DistanceMeters}m falls inside the safety zone of the obstacle at {obstacle.DistanceMeters}m (invalid coin path).");
                        break;
                    }
                }
            }
        }
    }
}
