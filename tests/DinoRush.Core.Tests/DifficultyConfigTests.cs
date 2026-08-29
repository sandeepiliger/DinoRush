using System;
using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class DifficultyConfigTests
    {
        [Test]
        public void CreateDefault_MatchesSpecTimeline()
        {
            var config = DifficultyConfig.CreateDefault();

            Assert.That(config.GetTierWindow(0).Tier, Is.EqualTo(DifficultyTier.Calm));
            Assert.That(config.GetTierWindow(29.9f).Tier, Is.EqualTo(DifficultyTier.Calm));
            Assert.That(config.GetTierWindow(30f).Tier, Is.EqualTo(DifficultyTier.Rising));
            Assert.That(config.GetTierWindow(60f).Tier, Is.EqualTo(DifficultyTier.Hazard));
            Assert.That(config.GetTierWindow(90f).Tier, Is.EqualTo(DifficultyTier.PreExtinction));
            Assert.That(config.GetTierWindow(120f).Tier, Is.EqualTo(DifficultyTier.Extinction));
            Assert.That(config.GetTierWindow(999f).Tier, Is.EqualTo(DifficultyTier.Extinction));
        }

        [Test]
        public void SpeedMultiplier_NeverDecreasesAcrossTiers()
        {
            var config = DifficultyConfig.CreateDefault();
            float previous = 0f;
            foreach (var tier in config.Tiers)
            {
                Assert.That(tier.RunSpeedMultiplier, Is.GreaterThanOrEqualTo(previous));
                previous = tier.RunSpeedMultiplier;
            }
        }

        [Test]
        public void Constructor_RejectsTiersNotStartingAtZero()
        {
            Assert.Throws<ArgumentException>(() => new DifficultyConfig(new[]
            {
                new DifficultyTierWindow(DifficultyTier.Calm, 5f, 1f),
            }));
        }

        [Test]
        public void Constructor_RejectsOutOfOrderTiers()
        {
            Assert.Throws<ArgumentException>(() => new DifficultyConfig(new[]
            {
                new DifficultyTierWindow(DifficultyTier.Calm, 0f, 1f),
                new DifficultyTierWindow(DifficultyTier.Rising, 10f, 1.1f),
                new DifficultyTierWindow(DifficultyTier.Hazard, 5f, 1.2f),
            }));
        }
    }
}
