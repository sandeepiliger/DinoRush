using System;

namespace DinoRush.Runtime
{
    // Decides what "today" means. Core deliberately never touches DateTime — daily rotation and
    // the daily challenge take an integer day index instead (docs/DECISIONS.md D9), which keeps
    // them testable and timezone-free. This is the one place the ambiguity gets resolved.
    public static class GameClock
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // UTC, not local time. Local time would let a player roll the daily set over repeatedly
        // by changing timezone, and would make "the same challenge for everyone" (section 21)
        // false across regions.
        //
        // This is still device-reported and therefore trivially manipulable by changing the
        // clock — section 20 asks to discourage that "where practical" but not to pretend it's
        // solved. Anything that must actually resist it needs a server timestamp, which is a
        // post-MVP concern (section 22).
        public static int TodayIndexUtc => (int)(DateTime.UtcNow.Date - Epoch).TotalDays;
    }
}
