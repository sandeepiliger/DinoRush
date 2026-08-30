using System;
using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class MissionEvaluatorTests
    {
        [Test]
        public void IsComplete_FalseUntilTargetReached()
        {
            var mission = new MissionDefinition("run_500m", MissionMetric.DistanceMeters, 500, 150);
            var progress = new MissionProgress(mission.Id);

            Assert.That(MissionEvaluator.IsComplete(mission, progress), Is.False);

            progress.Advance(499);
            Assert.That(MissionEvaluator.IsComplete(mission, progress), Is.False);

            progress.Advance(1);
            Assert.That(MissionEvaluator.IsComplete(mission, progress), Is.True);
        }

        [Test]
        public void Claim_ReturnsRewardAndMarksClaimed()
        {
            var mission = new MissionDefinition("collect_100_coins", MissionMetric.CoinsCollected, 100, 200);
            var progress = new MissionProgress(mission.Id, 100);

            int reward = MissionEvaluator.Claim(mission, progress);

            Assert.That(reward, Is.EqualTo(200));
            Assert.That(progress.IsClaimed, Is.True);
        }

        [Test]
        public void Claim_ThrowsIfNotYetComplete()
        {
            var mission = new MissionDefinition("survive_60s", MissionMetric.SurvivalSeconds, 60, 250);
            var progress = new MissionProgress(mission.Id, 10);

            Assert.Throws<InvalidOperationException>(() => MissionEvaluator.Claim(mission, progress));
        }

        [Test]
        public void Claim_ThrowsIfAlreadyClaimed()
        {
            var mission = new MissionDefinition("play_3_runs", MissionMetric.RunsPlayed, 3, 100);
            var progress = new MissionProgress(mission.Id, 3);

            MissionEvaluator.Claim(mission, progress);

            Assert.Throws<InvalidOperationException>(() => MissionEvaluator.Claim(mission, progress));
        }

        [Test]
        public void Claim_ThrowsIfMissionAndProgressIdsDoNotMatch()
        {
            var mission = new MissionDefinition("jump_20_obstacles", MissionMetric.ObstaclesCleared, 20, 200);
            var progress = new MissionProgress("a_different_mission_id", 20);

            Assert.Throws<ArgumentException>(() => MissionEvaluator.Claim(mission, progress));
        }
    }
}
