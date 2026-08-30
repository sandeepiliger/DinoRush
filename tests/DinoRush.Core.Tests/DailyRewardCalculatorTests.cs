using System;
using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class DailyRewardCalculatorTests
    {
        [Test]
        public void NewPlayer_CanClaimDayOne()
        {
            var cycle = DailyRewardCycle.CreateDefault();
            var state = DailyRewardState.NewPlayer();

            Assert.That(DailyRewardCalculator.CanClaim(state, 100), Is.True);

            var reward = DailyRewardCalculator.Claim(cycle, state, 100, out var next);

            Assert.That(reward.DayNumber, Is.EqualTo(1));
            Assert.That(next.CurrentStreakDay, Is.EqualTo(2));
            Assert.That(next.LastClaimedDayIndex, Is.EqualTo(100));
        }

        [Test]
        public void ClaimingTwiceOnTheSameDay_IsRejected()
        {
            var cycle = DailyRewardCycle.CreateDefault();
            var state = DailyRewardState.NewPlayer();
            DailyRewardCalculator.Claim(cycle, state, 100, out var afterFirstClaim);

            Assert.That(DailyRewardCalculator.CanClaim(afterFirstClaim, 100), Is.False);
            Assert.Throws<InvalidOperationException>(() => DailyRewardCalculator.Claim(cycle, afterFirstClaim, 100, out _));
        }

        [Test]
        public void ConsecutiveDays_AdvanceTheStreak()
        {
            var cycle = DailyRewardCycle.CreateDefault();
            var state = DailyRewardState.NewPlayer();

            for (int day = 1; day <= 7; day++)
            {
                var reward = DailyRewardCalculator.Claim(cycle, state, 100 + day, out state);
                Assert.That(reward.DayNumber, Is.EqualTo(day));
            }

            // Day 8 loops back to day 1, per CLAUDE.md section 20 ("Loop or start a new
            // reward cycle").
            var loopedReward = DailyRewardCalculator.Claim(cycle, state, 108, out _);
            Assert.That(loopedReward.DayNumber, Is.EqualTo(1));
        }

        [Test]
        public void MissingADay_ResetsTheStreak()
        {
            var cycle = DailyRewardCycle.CreateDefault();
            var state = DailyRewardState.NewPlayer();

            DailyRewardCalculator.Claim(cycle, state, 100, out state); // day 1
            DailyRewardCalculator.Claim(cycle, state, 101, out state); // day 2

            // Player skips several days.
            var reward = DailyRewardCalculator.Claim(cycle, state, 110, out state);

            Assert.That(reward.DayNumber, Is.EqualTo(1));
        }
    }
}
