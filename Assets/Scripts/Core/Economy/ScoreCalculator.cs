using System;

namespace DinoRush.Core
{
    public static class ScoreCalculator
    {
        // CLAUDE.md doesn't pin an exact formula. Distance is the primary driver — section 22
        // ("never trust client-side scores in a competitive global leaderboard") implies the
        // score must be a pure, auditable function of run telemetry, not an arbitrary
        // accumulator — with a small bonus for coins collected.
        public static int CalculateScore(float distanceMeters, int coinsCollected)
        {
            if (distanceMeters < 0) throw new ArgumentOutOfRangeException(nameof(distanceMeters));
            if (coinsCollected < 0) throw new ArgumentOutOfRangeException(nameof(coinsCollected));

            int distanceScore = (int)Math.Floor(distanceMeters);
            int coinBonus = coinsCollected * 2;
            return distanceScore + coinBonus;
        }
    }
}
