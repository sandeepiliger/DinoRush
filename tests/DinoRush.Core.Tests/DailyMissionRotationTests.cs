using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class DailyMissionRotationTests
    {
        [Test]
        public void FirstEverLaunchCountsAsARollover()
        {
            var save = SaveMigrator.CreateDefault();
            var tracker = new MissionTracker();

            Assert.That(DailyMissionRotation.EnsureCurrent(save, 20320, tracker), Is.True);
            Assert.That(save.DailyMissionDayIndex, Is.EqualTo(20320));
            Assert.That(tracker.Active.Count, Is.EqualTo(MissionCatalog.DailySetSize));
        }

        [Test]
        public void ReopeningOnTheSameDayKeepsProgress()
        {
            var save = SaveMigrator.CreateDefault();
            var tracker = new MissionTracker();
            DailyMissionRotation.EnsureCurrent(save, 20320, tracker);

            tracker.ApplyRun(new RunSummary(400f, 30, 25f, 12, 500));
            tracker.WriteTo(save);

            var reopened = new MissionTracker();
            bool rolled = DailyMissionRotation.EnsureCurrent(save, 20320, reopened);

            Assert.That(rolled, Is.False);
            foreach (var mission in reopened.Active)
                Assert.That(reopened.GetProgress(mission.Id).CurrentValue, Is.GreaterThan(0),
                    "Same-day progress should have been restored, not reset.");
        }

        [Test]
        public void ANewDayClearsYesterdaysCounters()
        {
            // The bug this exists to prevent: without a rollover, a fresh set would inherit
            // yesterday's totals and several missions would complete on sight.
            var save = SaveMigrator.CreateDefault();
            var tracker = new MissionTracker();
            DailyMissionRotation.EnsureCurrent(save, 20320, tracker);
            tracker.ApplyRun(new RunSummary(5000f, 900, 300f, 200, 9000));
            tracker.WriteTo(save);

            var today = new MissionTracker();
            bool rolled = DailyMissionRotation.EnsureCurrent(save, 20321, today);

            Assert.That(rolled, Is.True);
            foreach (var mission in today.Active)
            {
                Assert.That(today.GetProgress(mission.Id).CurrentValue, Is.Zero);
                Assert.That(today.IsComplete(mission), Is.False);
            }
        }

        [Test]
        public void RolloverPreservesLifetimeProgression()
        {
            // Coins, best score and unlocks are not daily state and must survive.
            var save = SaveMigrator.CreateDefault();
            save.Coins = 4200;
            save.BestScore = 8800;
            save.UnlockedDinosaurIds.Add("trex");

            DailyMissionRotation.EnsureCurrent(save, 20320, new MissionTracker());
            DailyMissionRotation.EnsureCurrent(save, 20321, new MissionTracker());

            Assert.That(save.Coins, Is.EqualTo(4200));
            Assert.That(save.BestScore, Is.EqualTo(8800));
            Assert.That(save.UnlockedDinosaurIds, Does.Contain("trex"));
        }

        [Test]
        public void SkippingSeveralDaysStillRollsOverExactlyOnce()
        {
            var save = SaveMigrator.CreateDefault();
            DailyMissionRotation.EnsureCurrent(save, 20320, new MissionTracker());

            Assert.That(DailyMissionRotation.EnsureCurrent(save, 20330, new MissionTracker()), Is.True);
            Assert.That(DailyMissionRotation.EnsureCurrent(save, 20330, new MissionTracker()), Is.False);
        }
    }
}
