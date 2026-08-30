using System;

namespace DinoRush.Core
{
    public enum ChallengeObjective
    {
        SurviveSeconds,
        ReachDistance,
        CollectCoins,
        ClearObstacles,
    }

    // One challenge per day, identical for every player — CLAUDE.md section 21. That property
    // is what makes a future leaderboard meaningful: comparing scores only means anything if
    // everyone ran the same track.
    //
    // Both the objective and the run's seed derive from the date alone, with no server
    // involved, so this works offline (section 54) and stays consistent across devices.
    public sealed class DailyChallenge
    {
        public int DayIndex { get; }
        public int Seed { get; }
        public ChallengeObjective Objective { get; }
        public int TargetValue { get; }
        public int CoinReward { get; }

        private DailyChallenge(int dayIndex, int seed, ChallengeObjective objective, int targetValue, int coinReward)
        {
            DayIndex = dayIndex;
            Seed = seed;
            Objective = objective;
            TargetValue = targetValue;
            CoinReward = coinReward;
        }

        public static DailyChallenge ForDay(int dayIndex)
        {
            // A different multiplier from MissionCatalog's, so the day's challenge and the day's
            // mission set don't move in lockstep.
            var random = new SeededRandom(unchecked(dayIndex * 6607 + 101));

            var objective = (ChallengeObjective)random.NextInt(0, 4);
            int target;
            int reward;

            switch (objective)
            {
                case ChallengeObjective.SurviveSeconds:
                    target = 60 + random.NextInt(0, 5) * 15;   // 60..120s
                    reward = 800 + target * 4;
                    break;
                case ChallengeObjective.ReachDistance:
                    target = 800 + random.NextInt(0, 9) * 200; // 800..2400m
                    reward = 800 + target / 4;
                    break;
                case ChallengeObjective.CollectCoins:
                    target = 40 + random.NextInt(0, 7) * 15;   // 40..130
                    reward = 700 + target * 6;
                    break;
                default:
                    target = 25 + random.NextInt(0, 8) * 5;    // 25..60
                    reward = 700 + target * 12;
                    break;
            }

            // The run seed is drawn from the same stream, so the day fully determines the track.
            int seed = unchecked((int)random.NextUInt64());

            return new DailyChallenge(dayIndex, seed, objective, target, reward);
        }

        public bool IsSatisfiedBy(RunSummary summary)
        {
            switch (Objective)
            {
                case ChallengeObjective.SurviveSeconds: return summary.SurvivalSeconds >= TargetValue;
                case ChallengeObjective.ReachDistance: return summary.DistanceMeters >= TargetValue;
                case ChallengeObjective.CollectCoins: return summary.CoinsCollected >= TargetValue;
                case ChallengeObjective.ClearObstacles: return summary.ObstaclesCleared >= TargetValue;
                default: throw new InvalidOperationException($"Unhandled objective {Objective}.");
            }
        }

        public string Describe()
        {
            switch (Objective)
            {
                case ChallengeObjective.SurviveSeconds: return $"Survive {TargetValue} seconds";
                case ChallengeObjective.ReachDistance: return $"Reach {TargetValue} metres";
                case ChallengeObjective.CollectCoins: return $"Collect {TargetValue} coins";
                case ChallengeObjective.ClearObstacles: return $"Clear {TargetValue} obstacles";
                default: throw new InvalidOperationException($"Unhandled objective {Objective}.");
            }
        }
    }
}
