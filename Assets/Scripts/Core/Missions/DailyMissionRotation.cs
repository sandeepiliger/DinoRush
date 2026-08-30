using System;

namespace DinoRush.Core
{
    // Decides when the daily mission set rolls over, and clears the previous day's counters.
    //
    // Without this, a new day's missions would inherit yesterday's progress and several would
    // complete the instant they appeared. Kept in Core and driven by an injected day index
    // rather than DateTime so it is testable and timezone-free — the Runtime layer decides what
    // "today" means (see GameClock).
    public static class DailyMissionRotation
    {
        // Returns true when the set rolled over, so the caller can tell the player their
        // missions are new rather than silently resetting their progress.
        public static bool EnsureCurrent(SaveDataV1 save, int todayDayIndex, MissionTracker tracker)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (tracker == null) throw new ArgumentNullException(nameof(tracker));

            bool rolledOver = save.DailyMissionDayIndex != todayDayIndex;

            if (rolledOver)
            {
                // Only daily counters are cleared. Coins, best score and unlocks are lifetime
                // progression and must survive the rollover untouched.
                save.MissionProgress.Clear();
                save.MissionClaimed.Clear();
                save.DailyMissionDayIndex = todayDayIndex;
            }

            tracker.SetActiveMissions(MissionCatalog.GetDailySet(todayDayIndex), save);
            return rolledOver;
        }
    }
}
