using System;

namespace DinoRush.Core
{
    public enum MissionMetric
    {
        DistanceMeters,
        CoinsCollected,
        ObstaclesCleared,
        RunsPlayed,
        SurvivalSeconds,
        PowerUpsUsed,
        RewardedAdsWatched,
    }

    public sealed class MissionDefinition
    {
        public string Id { get; }
        public MissionMetric Metric { get; }
        public int TargetValue { get; }
        public int CoinReward { get; }

        public MissionDefinition(string id, MissionMetric metric, int targetValue, int coinReward)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Mission id is required.", nameof(id));
            if (targetValue <= 0) throw new ArgumentOutOfRangeException(nameof(targetValue));
            if (coinReward < 0) throw new ArgumentOutOfRangeException(nameof(coinReward));

            Id = id;
            Metric = metric;
            TargetValue = targetValue;
            CoinReward = coinReward;
        }
    }

    public sealed class MissionProgress
    {
        public string MissionId { get; }
        public int CurrentValue { get; private set; }
        public bool IsClaimed { get; private set; }

        public MissionProgress(string missionId, int currentValue = 0, bool isClaimed = false)
        {
            if (string.IsNullOrWhiteSpace(missionId)) throw new ArgumentException("Mission id is required.", nameof(missionId));
            if (currentValue < 0) throw new ArgumentOutOfRangeException(nameof(currentValue));

            MissionId = missionId;
            CurrentValue = currentValue;
            IsClaimed = isClaimed;
        }

        public void Advance(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            CurrentValue += amount;
        }

        internal void MarkClaimed() => IsClaimed = true;
    }

    // Pure evaluation logic, data-driven per CLAUDE.md section 19 ("adding a new mission
    // should not require modifying core gameplay code") — adding a mission means adding a
    // MissionDefinition, never touching this evaluator.
    public static class MissionEvaluator
    {
        public static bool IsComplete(MissionDefinition definition, MissionProgress progress)
        {
            RequireMatch(definition, progress);
            return progress.CurrentValue >= definition.TargetValue;
        }

        // Returns the coin reward and marks the mission claimed, or throws if it isn't
        // complete yet or was already claimed — callers must check IsComplete first.
        public static int Claim(MissionDefinition definition, MissionProgress progress)
        {
            RequireMatch(definition, progress);
            if (progress.IsClaimed) throw new InvalidOperationException($"Mission '{definition.Id}' was already claimed.");
            if (!IsComplete(definition, progress)) throw new InvalidOperationException($"Mission '{definition.Id}' is not complete yet.");

            progress.MarkClaimed();
            return definition.CoinReward;
        }

        private static void RequireMatch(MissionDefinition definition, MissionProgress progress)
        {
            if (definition.Id != progress.MissionId)
                throw new ArgumentException($"Progress for '{progress.MissionId}' does not match mission '{definition.Id}'.");
        }
    }
}
