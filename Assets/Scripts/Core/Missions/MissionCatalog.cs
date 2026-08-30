using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // The mission set, as data. CLAUDE.md section 19 requires that adding a mission not touch
    // gameplay code — every mission here is a MissionDefinition consumed by the generic
    // MissionEvaluator, so a new one is a new list entry and nothing else.
    //
    // Twelve missions rather than section 75's fifty, per docs/DECISIONS.md D6. The daily set
    // draws from these; the remainder ship as content once the loop is proven.
    public static class MissionCatalog
    {
        public const int DailySetSize = 4;

        public static IReadOnlyList<MissionDefinition> All { get; } = new[]
        {
            new MissionDefinition("run_500m", MissionMetric.DistanceMeters, 500, 150),
            new MissionDefinition("run_1500m", MissionMetric.DistanceMeters, 1500, 300),
            new MissionDefinition("run_3000m", MissionMetric.DistanceMeters, 3000, 500),
            new MissionDefinition("collect_50_coins", MissionMetric.CoinsCollected, 50, 120),
            new MissionDefinition("collect_150_coins", MissionMetric.CoinsCollected, 150, 250),
            new MissionDefinition("collect_400_coins", MissionMetric.CoinsCollected, 400, 450),
            new MissionDefinition("clear_20_obstacles", MissionMetric.ObstaclesCleared, 20, 200),
            new MissionDefinition("clear_60_obstacles", MissionMetric.ObstaclesCleared, 60, 350),
            new MissionDefinition("survive_60s", MissionMetric.SurvivalSeconds, 60, 250),
            new MissionDefinition("survive_120s", MissionMetric.SurvivalSeconds, 120, 400),
            new MissionDefinition("play_3_runs", MissionMetric.RunsPlayed, 3, 100),
            new MissionDefinition("play_10_runs", MissionMetric.RunsPlayed, 10, 300),
        };

        public static MissionDefinition Get(string id)
        {
            foreach (var mission in All)
                if (mission.Id == id) return mission;

            throw new ArgumentOutOfRangeException(nameof(id), $"No mission defined with id '{id}'.");
        }

        public static bool TryGet(string id, out MissionDefinition mission)
        {
            foreach (var candidate in All)
            {
                if (candidate.Id == id)
                {
                    mission = candidate;
                    return true;
                }
            }
            mission = null;
            return false;
        }

        // The day's missions, chosen deterministically from the date so every player on a given
        // day sees the same set — the same property section 21 requires of the daily challenge,
        // and a prerequisite for ever comparing progress between players.
        //
        // Picks without replacement so a set can't contain the same mission twice.
        public static IReadOnlyList<MissionDefinition> GetDailySet(int dayIndex, int size = DailySetSize)
        {
            if (size <= 0 || size > All.Count)
                throw new ArgumentOutOfRangeException(nameof(size));

            var pool = new List<MissionDefinition>(All);
            var random = new SeededRandom(unchecked(dayIndex * 7919 + 13));
            var chosen = new List<MissionDefinition>(size);

            for (int i = 0; i < size; i++)
            {
                int index = random.NextInt(0, pool.Count);
                chosen.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return chosen;
        }
    }
}
