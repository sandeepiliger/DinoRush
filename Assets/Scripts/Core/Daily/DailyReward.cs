using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    public sealed class DailyRewardDay
    {
        public int DayNumber { get; } // 1..7
        public int CoinReward { get; }
        public string SpecialRewardId { get; } // null for a plain-coin day

        public DailyRewardDay(int dayNumber, int coinReward, string specialRewardId = null)
        {
            if (dayNumber < 1 || dayNumber > 7) throw new ArgumentOutOfRangeException(nameof(dayNumber));
            if (coinReward < 0) throw new ArgumentOutOfRangeException(nameof(coinReward));

            DayNumber = dayNumber;
            CoinReward = coinReward;
            SpecialRewardId = specialRewardId;
        }
    }

    public sealed class DailyRewardCycle
    {
        public IReadOnlyList<DailyRewardDay> Days { get; }

        public DailyRewardCycle(IReadOnlyList<DailyRewardDay> days)
        {
            if (days == null || days.Count != 7)
                throw new ArgumentException("A daily reward cycle must define exactly 7 days.", nameof(days));
            for (int i = 0; i < 7; i++)
            {
                if (days[i].DayNumber != i + 1)
                    throw new ArgumentException("Days must be ordered 1..7 with no gaps.", nameof(days));
            }

            Days = days;
        }

        public DailyRewardDay GetDay(int dayNumber) => Days[(dayNumber - 1 + 7) % 7];

        // Matches the example cycle in CLAUDE.md section 20.
        public static DailyRewardCycle CreateDefault() => new DailyRewardCycle(new[]
        {
            new DailyRewardDay(1, 100),
            new DailyRewardDay(2, 150),
            new DailyRewardDay(3, 0, "rare_box"),
            new DailyRewardDay(4, 250),
            new DailyRewardDay(5, 0, "gems_15"),
            new DailyRewardDay(6, 0, "skin_fragment"),
            new DailyRewardDay(7, 2000, "legendary_skin"),
        });
    }

    public sealed class DailyRewardState
    {
        public int CurrentStreakDay { get; } // 1..7, the day about to be claimed

        // An opaque, caller-defined, monotonically non-decreasing day counter (e.g. days since
        // some epoch). Core never touches DateTime/DateOnly itself: DateOnly isn't available on
        // netstandard2.1 (this assembly's target — see docs/DECISIONS.md D9), and keeping
        // calendar logic out of Core also sidesteps timezone questions entirely.
        public int? LastClaimedDayIndex { get; }

        public DailyRewardState(int currentStreakDay, int? lastClaimedDayIndex)
        {
            if (currentStreakDay < 1 || currentStreakDay > 7) throw new ArgumentOutOfRangeException(nameof(currentStreakDay));

            CurrentStreakDay = currentStreakDay;
            LastClaimedDayIndex = lastClaimedDayIndex;
        }

        public static DailyRewardState NewPlayer() => new DailyRewardState(1, null);
    }

    public static class DailyRewardCalculator
    {
        public static bool CanClaim(DailyRewardState state, int todayDayIndex)
        {
            return state.LastClaimedDayIndex is not int last || last != todayDayIndex;
        }

        public static DailyRewardDay Claim(DailyRewardCycle cycle, DailyRewardState state, int todayDayIndex, out DailyRewardState nextState)
        {
            if (!CanClaim(state, todayDayIndex))
                throw new InvalidOperationException("The daily reward has already been claimed today.");

            int dayToClaim = state.CurrentStreakDay;
            if (state.LastClaimedDayIndex is int last)
            {
                int gap = todayDayIndex - last;
                if (gap < 0)
                    throw new ArgumentException("todayDayIndex must not precede the last claim.", nameof(todayDayIndex));
                if (gap > 1)
                    dayToClaim = 1; // missed a day (or more) — streak resets, per section 20
                // gap == 1 keeps the streak continuing at its current day.
            }

            var reward = cycle.GetDay(dayToClaim);
            int nextDay = dayToClaim >= 7 ? 1 : dayToClaim + 1; // loop after day 7, per section 20
            nextState = new DailyRewardState(nextDay, todayDayIndex);
            return reward;
        }
    }
}
