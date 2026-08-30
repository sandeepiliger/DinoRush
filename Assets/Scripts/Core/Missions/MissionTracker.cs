using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // Applies a finished run to a set of missions. Generic over MissionMetric so adding a
    // mission type means adding an enum case and one line here, never touching gameplay code
    // (CLAUDE.md section 19).
    public sealed class MissionTracker
    {
        private readonly Dictionary<string, MissionProgress> _progress = new Dictionary<string, MissionProgress>(StringComparer.Ordinal);
        private readonly List<MissionDefinition> _active = new List<MissionDefinition>();

        public IReadOnlyList<MissionDefinition> Active => _active;

        public void SetActiveMissions(IReadOnlyList<MissionDefinition> missions, SaveDataV1 save)
        {
            if (missions == null) throw new ArgumentNullException(nameof(missions));
            if (save == null) throw new ArgumentNullException(nameof(save));

            _active.Clear();
            _progress.Clear();

            foreach (var mission in missions)
            {
                _active.Add(mission);

                save.MissionProgress.TryGetValue(mission.Id, out int stored);
                save.MissionClaimed.TryGetValue(mission.Id, out bool claimed);
                _progress[mission.Id] = new MissionProgress(mission.Id, stored, claimed);
            }
        }

        public MissionProgress GetProgress(string missionId) =>
            _progress.TryGetValue(missionId, out var progress) ? progress : null;

        public bool IsComplete(MissionDefinition mission) =>
            _progress.TryGetValue(mission.Id, out var progress) && MissionEvaluator.IsComplete(mission, progress);

        // Advances every active mission by what the run delivered, and reports the ones that
        // crossed their target on this run — the Game Over screen shows exactly those.
        public IReadOnlyList<MissionDefinition> ApplyRun(RunSummary summary)
        {
            var newlyCompleted = new List<MissionDefinition>();

            foreach (var mission in _active)
            {
                var progress = _progress[mission.Id];
                if (progress.IsClaimed) continue;

                bool wasComplete = MissionEvaluator.IsComplete(mission, progress);
                progress.Advance(AmountFor(mission.Metric, summary));

                if (!wasComplete && MissionEvaluator.IsComplete(mission, progress))
                    newlyCompleted.Add(mission);
            }

            return newlyCompleted;
        }

        // Claims a completed mission, returning the coins awarded. Throws if it isn't complete
        // or was already claimed — those are programming errors, not player-facing states.
        public int Claim(MissionDefinition mission)
        {
            if (!_progress.TryGetValue(mission.Id, out var progress))
                throw new ArgumentException($"Mission '{mission.Id}' is not active.", nameof(mission));

            return MissionEvaluator.Claim(mission, progress);
        }

        // Writes progress back into the save. Called after a run, alongside the coin bank.
        public void WriteTo(SaveDataV1 save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            foreach (var mission in _active)
            {
                var progress = _progress[mission.Id];
                save.MissionProgress[mission.Id] = progress.CurrentValue;
                save.MissionClaimed[mission.Id] = progress.IsClaimed;
            }
        }

        private static int AmountFor(MissionMetric metric, RunSummary summary)
        {
            switch (metric)
            {
                case MissionMetric.DistanceMeters: return (int)summary.DistanceMeters;
                case MissionMetric.CoinsCollected: return summary.CoinsCollected;
                case MissionMetric.ObstaclesCleared: return summary.ObstaclesCleared;
                case MissionMetric.SurvivalSeconds: return (int)summary.SurvivalSeconds;
                case MissionMetric.RunsPlayed: return 1;

                // Not driven by a run's telemetry — power-ups and rewarded ads report
                // themselves when they happen, so a run contributes nothing here.
                case MissionMetric.PowerUpsUsed:
                case MissionMetric.RewardedAdsWatched:
                    return 0;

                default:
                    throw new ArgumentOutOfRangeException(nameof(metric), $"Unhandled mission metric {metric}.");
            }
        }
    }
}
