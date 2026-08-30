using System;
using System.Collections.Generic;
using System.Linq;
using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class AnalyticsTests
    {
        [Test]
        public void EveryEventHasAWireName()
        {
            // A missing mapping would throw at the moment the event fired — in production,
            // during whatever moment it was meant to measure.
            foreach (AnalyticsEvent value in Enum.GetValues(typeof(AnalyticsEvent)))
                Assert.DoesNotThrow(() => AnalyticsEventNames.Of(value), $"{value} has no wire name.");
        }

        [Test]
        public void WireNamesAreUniqueAndSnakeCase()
        {
            var names = Enum.GetValues(typeof(AnalyticsEvent))
                .Cast<AnalyticsEvent>()
                .Select(AnalyticsEventNames.Of)
                .ToList();

            Assert.That(names.Distinct().Count(), Is.EqualTo(names.Count),
                "Two events share a wire name — their metrics would be merged.");

            foreach (var name in names)
                Assert.That(name, Does.Match("^[a-z][a-z0-9_]*$"), $"'{name}' is not snake_case.");
        }

        [Test]
        public void SpecifiedEventNamesMatchSection28Exactly()
        {
            // These strings are quoted verbatim in the spec; a dashboard built against them
            // would silently lose data if one drifted.
            Assert.That(AnalyticsEventNames.Of(AnalyticsEvent.FirstOpen), Is.EqualTo("first_open"));
            Assert.That(AnalyticsEventNames.Of(AnalyticsEvent.RunStarted), Is.EqualTo("run_started"));
            Assert.That(AnalyticsEventNames.Of(AnalyticsEvent.PlayerDied), Is.EqualTo("player_died"));
            Assert.That(AnalyticsEventNames.Of(AnalyticsEvent.RewardedAdCompleted), Is.EqualTo("rewarded_ad_completed"));
            Assert.That(AnalyticsEventNames.Of(AnalyticsEvent.PurchaseCompleted), Is.EqualTo("purchase_completed"));
            Assert.That(AnalyticsEventNames.Of(AnalyticsEvent.DayReturn), Is.EqualTo("day_return"));
        }

        [Test]
        public void EventsReachTheProviderWithTheirParameters()
        {
            var provider = new MockAnalyticsProvider();
            var analytics = new AnalyticsManager(provider);

            analytics.Track(AnalyticsEvent.DistanceReached, "distance", 1200);

            Assert.That(provider.Events, Has.Count.EqualTo(1));
            Assert.That(provider.Events[0].Name, Is.EqualTo("distance_reached"));
            Assert.That(provider.Events[0].Parameters["distance"], Is.EqualTo(1200));
        }

        [Test]
        public void AFailingProviderNeverThrowsIntoGameplay()
        {
            // Section 55: "Analytics unavailable: continue normally."
            var provider = new MockAnalyticsProvider { ThrowOnTrack = true };
            var analytics = new AnalyticsManager(provider);

            Assert.DoesNotThrow(() => analytics.Track(AnalyticsEvent.RunStarted));
            Assert.That(analytics.DroppedEventCount, Is.EqualTo(1),
                "Dropped events should be counted, not silently discarded.");
        }

        [Test]
        public void SessionsAreNeverDoubleCounted()
        {
            // Android delivers repeated resume callbacks as a matter of course; double-counting
            // would corrupt every retention metric derived from sessions.
            var provider = new MockAnalyticsProvider();
            var analytics = new AnalyticsManager(provider);

            analytics.BeginSession();
            analytics.BeginSession();
            analytics.BeginSession();

            Assert.That(provider.CountOf("session_started"), Is.EqualTo(1));
        }

        [Test]
        public void EndingASessionThatNeverStartedDoesNothing()
        {
            var provider = new MockAnalyticsProvider();
            var analytics = new AnalyticsManager(provider);

            analytics.EndSession();

            Assert.That(provider.CountOf("session_ended"), Is.Zero);
        }

        [Test]
        public void SessionsCanBeReopenedAfterEnding()
        {
            var provider = new MockAnalyticsProvider();
            var analytics = new AnalyticsManager(provider);

            analytics.BeginSession();
            analytics.EndSession();
            analytics.BeginSession();

            Assert.That(provider.CountOf("session_started"), Is.EqualTo(2));
            Assert.That(analytics.IsSessionActive, Is.True);
        }
    }

    [TestFixture]
    public class GameConfigTests
    {
        [Test]
        public void NoProviderReturnsTheFallback()
        {
            // What "no remote config yet" looks like — and it must simply work.
            var config = new GameConfig();

            Assert.That(config.GetFloat(ConfigKeys.StartingSpeed, 8f), Is.EqualTo(8f));
            Assert.That(config.GetBool(ConfigKeys.ReviveEnabled, true), Is.True);
        }

        [Test]
        public void UnknownKeysReturnTheFallback()
        {
            var config = new GameConfig(new LocalGameConfigProvider());

            Assert.That(config.GetInt("a_key_that_does_not_exist", 42), Is.EqualTo(42));
        }

        [Test]
        public void ProvidedValuesWinOverFallbacks()
        {
            var provider = new LocalGameConfigProvider();
            provider.Set(ConfigKeys.CoinRewardMultiplier, 2.5f);
            var config = new GameConfig(provider);

            Assert.That(config.GetFloat(ConfigKeys.CoinRewardMultiplier, 1f), Is.EqualTo(2.5f));
        }

        [Test]
        public void LocalDefaultsMatchTheConstantsTheSystemsUse()
        {
            // The local provider must be a no-op until someone deliberately overrides
            // something; if it disagreed with the code's own constants, merely introducing it
            // would change the game's balance.
            var config = new GameConfig(new LocalGameConfigProvider());

            Assert.That(config.GetInt(ConfigKeys.InterstitialRunsBetween, -1),
                Is.EqualTo(InterstitialPolicy.RunsBetweenAds));
            Assert.That(config.GetInt(ConfigKeys.InterstitialMinimumRuns, -1),
                Is.EqualTo(InterstitialPolicy.MinimumRunsBeforeFirstAd));
        }

        [Test]
        public void AThrowingProviderCannotAlterBalance()
        {
            // A remote provider that fails must not be able to change tuning by failing.
            var config = new GameConfig(new ThrowingConfigProvider());

            Assert.That(config.GetFloat(ConfigKeys.StartingSpeed, 8f), Is.EqualTo(8f));
            Assert.That(config.GetInt(ConfigKeys.InterstitialRunsBetween, 3), Is.EqualTo(3));
            Assert.That(config.GetBool(ConfigKeys.ShopEnabled, true), Is.True);
        }

        [Test]
        public void IntDefaultsAreReadableAsFloats()
        {
            var provider = new LocalGameConfigProvider();
            provider.Set("some_int", 7);
            var config = new GameConfig(provider);

            Assert.That(config.GetFloat("some_int", 0f), Is.EqualTo(7f));
        }

        [Test]
        public void EveryDeclaredKeyHasALocalDefault()
        {
            // Section 52 lists what should be configurable; a key with no default would fall
            // back to whatever each call site happened to pass.
            var provider = new LocalGameConfigProvider();
            var keys = typeof(ConfigKeys)
                .GetFields()
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue());

            foreach (var key in keys)
            {
                bool found = provider.TryGetFloat(key, out _)
                          || provider.TryGetInt(key, out _)
                          || provider.TryGetBool(key, out _);
                Assert.That(found, Is.True, $"Config key '{key}' has no local default.");
            }
        }

        private sealed class ThrowingConfigProvider : IGameConfigProvider
        {
            public bool TryGetFloat(string key, out float value) => throw new InvalidOperationException();
            public bool TryGetInt(string key, out int value) => throw new InvalidOperationException();
            public bool TryGetBool(string key, out bool value) => throw new InvalidOperationException();
        }
    }
}
