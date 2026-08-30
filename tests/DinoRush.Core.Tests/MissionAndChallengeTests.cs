using System.Collections.Generic;
using System.Linq;
using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class MissionCatalogTests
    {
        [Test]
        public void EveryMissionIdIsUnique()
        {
            var ids = MissionCatalog.All.Select(m => m.Id).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
        }

        [Test]
        public void DailySetIsTheSameForEveryPlayerOnAGivenDay()
        {
            // The property a shared leaderboard would depend on.
            var first = MissionCatalog.GetDailySet(20320).Select(m => m.Id);
            var second = MissionCatalog.GetDailySet(20320).Select(m => m.Id);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void DailySetChangesFromDayToDay()
        {
            var today = MissionCatalog.GetDailySet(20320).Select(m => m.Id).ToList();

            bool anyDifferent = false;
            for (int offset = 1; offset <= 5; offset++)
            {
                var other = MissionCatalog.GetDailySet(20320 + offset).Select(m => m.Id).ToList();
                if (!other.SequenceEqual(today)) anyDifferent = true;
            }

            Assert.That(anyDifferent, Is.True, "The daily set never changed across five days.");
        }

        [Test]
        public void DailySetNeverRepeatsAMissionWithinTheSameDay()
        {
            for (int day = 0; day < 200; day++)
            {
                var ids = MissionCatalog.GetDailySet(day).Select(m => m.Id).ToList();
                Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count), $"Day {day} contained a duplicate mission.");
                Assert.That(ids.Count, Is.EqualTo(MissionCatalog.DailySetSize));
            }
        }
    }

    [TestFixture]
    public class MissionTrackerTests
    {
        private static (MissionTracker tracker, SaveDataV1 save) NewTracker(params string[] missionIds)
        {
            var save = SaveMigrator.CreateDefault();
            var tracker = new MissionTracker();
            tracker.SetActiveMissions(missionIds.Select(MissionCatalog.Get).ToList(), save);
            return (tracker, save);
        }

        [Test]
        public void ARunAdvancesEveryRelevantMetric()
        {
            var (tracker, _) = NewTracker("run_500m", "collect_50_coins", "clear_20_obstacles", "survive_60s");

            tracker.ApplyRun(new RunSummary(600f, 60, 70f, 25, 900));

            Assert.That(tracker.IsComplete(MissionCatalog.Get("run_500m")), Is.True);
            Assert.That(tracker.IsComplete(MissionCatalog.Get("collect_50_coins")), Is.True);
            Assert.That(tracker.IsComplete(MissionCatalog.Get("clear_20_obstacles")), Is.True);
            Assert.That(tracker.IsComplete(MissionCatalog.Get("survive_60s")), Is.True);
        }

        [Test]
        public void ProgressAccumulatesAcrossRuns()
        {
            var (tracker, _) = NewTracker("collect_150_coins");
            var mission = MissionCatalog.Get("collect_150_coins");

            tracker.ApplyRun(new RunSummary(100f, 80, 20f, 5, 100));
            Assert.That(tracker.IsComplete(mission), Is.False);

            tracker.ApplyRun(new RunSummary(100f, 80, 20f, 5, 100));
            Assert.That(tracker.IsComplete(mission), Is.True);
        }

        [Test]
        public void RunsPlayedCountsOnePerRunRegardlessOfPerformance()
        {
            var (tracker, _) = NewTracker("play_3_runs");
            var mission = MissionCatalog.Get("play_3_runs");

            for (int i = 0; i < 2; i++) tracker.ApplyRun(new RunSummary(0f, 0, 0f, 0, 0));
            Assert.That(tracker.IsComplete(mission), Is.False);

            tracker.ApplyRun(new RunSummary(0f, 0, 0f, 0, 0));
            Assert.That(tracker.IsComplete(mission), Is.True);
        }

        [Test]
        public void OnlyNewlyCompletedMissionsAreReported()
        {
            // The Game Over screen shows what this run achieved, so a mission finished two runs
            // ago must not reappear.
            var (tracker, _) = NewTracker("run_500m");

            var first = tracker.ApplyRun(new RunSummary(600f, 0, 0f, 0, 0));
            Assert.That(first.Select(m => m.Id), Is.EqualTo(new[] { "run_500m" }));

            var second = tracker.ApplyRun(new RunSummary(600f, 0, 0f, 0, 0));
            Assert.That(second, Is.Empty);
        }

        [Test]
        public void ClaimingPaysOutOnceAndStops()
        {
            var (tracker, _) = NewTracker("run_500m");
            var mission = MissionCatalog.Get("run_500m");
            tracker.ApplyRun(new RunSummary(600f, 0, 0f, 0, 0));

            Assert.That(tracker.Claim(mission), Is.EqualTo(mission.CoinReward));
            Assert.Throws<System.InvalidOperationException>(() => tracker.Claim(mission));
        }

        [Test]
        public void AClaimedMissionStopsAccumulating()
        {
            var (tracker, _) = NewTracker("run_500m");
            var mission = MissionCatalog.Get("run_500m");
            tracker.ApplyRun(new RunSummary(600f, 0, 0f, 0, 0));
            tracker.Claim(mission);

            int atClaim = tracker.GetProgress(mission.Id).CurrentValue;
            tracker.ApplyRun(new RunSummary(600f, 0, 0f, 0, 0));

            Assert.That(tracker.GetProgress(mission.Id).CurrentValue, Is.EqualTo(atClaim));
        }

        [Test]
        public void ProgressSurvivesASaveRoundTrip()
        {
            var save = SaveMigrator.CreateDefault();
            var tracker = new MissionTracker();
            var missions = new List<MissionDefinition> { MissionCatalog.Get("collect_150_coins") };

            tracker.SetActiveMissions(missions, save);
            tracker.ApplyRun(new RunSummary(0f, 90, 0f, 0, 0));
            tracker.WriteTo(save);

            SaveSerializer.TryDeserialize(SaveSerializer.Serialize(save), out var reloaded);
            var restored = new MissionTracker();
            restored.SetActiveMissions(missions, reloaded);

            Assert.That(restored.GetProgress("collect_150_coins").CurrentValue, Is.EqualTo(90));
        }
    }

    [TestFixture]
    public class DailyChallengeTests
    {
        [Test]
        public void TheSameDayProducesTheSameChallengeAndTrack()
        {
            // Section 21's core requirement, and the precondition for a fair leaderboard.
            var a = DailyChallenge.ForDay(20320);
            var b = DailyChallenge.ForDay(20320);

            Assert.That(b.Objective, Is.EqualTo(a.Objective));
            Assert.That(b.TargetValue, Is.EqualTo(a.TargetValue));
            Assert.That(b.Seed, Is.EqualTo(a.Seed));
            Assert.That(b.CoinReward, Is.EqualTo(a.CoinReward));
        }

        [Test]
        public void DifferentDaysProduceDifferentTracks()
        {
            var seeds = Enumerable.Range(20320, 30).Select(d => DailyChallenge.ForDay(d).Seed).ToList();
            Assert.That(seeds.Distinct().Count(), Is.GreaterThan(25), "Daily seeds are colliding far too often.");
        }

        [Test]
        public void EveryObjectiveKindEventuallyAppears()
        {
            var objectives = Enumerable.Range(0, 400).Select(d => DailyChallenge.ForDay(d).Objective).Distinct().ToList();

            Assert.That(objectives.Count, Is.EqualTo(4),
                "Some challenge objectives are unreachable — players would never see them.");
        }

        [Test]
        public void ChallengeSeedsProduceValidRunsLikeAnyOther()
        {
            // A daily challenge that generated an unfair track would be visible to every player
            // simultaneously, so it gets the same section 48 guarantee as a normal run.
            var config = RunGenerationConfig.CreateDefault();
            var generator = new SegmentGenerator(config);
            var validator = new RunValidator(config);

            for (int day = 0; day < 120; day++)
            {
                var run = generator.GenerateRun(DailyChallenge.ForDay(day).Seed, 2500f);
                Assert.That(validator.Validate(run).IsValid, Is.True, $"Day {day}'s challenge track was invalid.");
            }
        }

        [Test]
        public void ObjectivesAreEvaluatedAgainstTheRightMetric()
        {
            var challenge = DailyChallenge.ForDay(20320);
            var short_ = new RunSummary(0f, 0, 0f, 0, 0);
            var generous = new RunSummary(99999f, 99999, 99999f, 99999, 99999);

            Assert.That(challenge.IsSatisfiedBy(short_), Is.False);
            Assert.That(challenge.IsSatisfiedBy(generous), Is.True);
        }

        [Test]
        public void EveryChallengeHasAReadableDescription()
        {
            for (int day = 0; day < 60; day++)
            {
                var text = DailyChallenge.ForDay(day).Describe();
                Assert.That(text, Is.Not.Null.And.Not.Empty);
                Assert.That(text, Does.Match(@"\d"), "A challenge description should state its target.");
            }
        }
    }
}
