using System.Collections.Generic;
using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class InterstitialPolicyTests
    {
        private static InterstitialPolicy PolicyAfterRuns(int runs)
        {
            var policy = new InterstitialPolicy();
            for (int i = 0; i < runs; i++) policy.RegisterRunCompleted();
            policy.Tick(600f); // long past any rewarded cooldown
            return policy;
        }

        [Test]
        public void NeverShownOnLaunchOrToANewPlayer()
        {
            // Section 24 forbids an interstitial immediately on launch, and section 27's whole
            // posture is that a new player must not be interrupted before they're hooked.
            for (int runs = 0; runs < InterstitialPolicy.MinimumRunsBeforeFirstAd; runs++)
                Assert.That(PolicyAfterRuns(runs).CanShow(GameState.GameOver), Is.False, $"Shown after only {runs} runs.");
        }

        [Test]
        public void NeverShownDuringGameplay()
        {
            var policy = PolicyAfterRuns(20);

            foreach (var state in new[] { GameState.Playing, GameState.Paused, GameState.Ready, GameState.Revive, GameState.Menu })
                Assert.That(policy.CanShow(state), Is.False, $"An interstitial was permitted during {state}.");

            Assert.That(policy.CanShow(GameState.GameOver), Is.True);
        }

        [Test]
        public void NeverShownImmediatelyAfterARewardedAd()
        {
            // Watching a rewarded ad and being served an interstitial seconds later is what
            // makes the rewarded offer feel like bait.
            var policy = PolicyAfterRuns(20);
            policy.RegisterRewardedShown();

            Assert.That(policy.CanShow(GameState.GameOver), Is.False);

            policy.Tick(InterstitialPolicy.MinimumSecondsAfterRewarded + 1f);
            Assert.That(policy.CanShow(GameState.GameOver), Is.True);
        }

        [Test]
        public void FrequencyCapSpacesAdsOut()
        {
            var policy = PolicyAfterRuns(20);
            Assert.That(policy.CanShow(GameState.GameOver), Is.True);
            policy.RegisterInterstitialShown();

            Assert.That(policy.CanShow(GameState.GameOver), Is.False, "Two interstitials in a row.");

            for (int i = 0; i < InterstitialPolicy.RunsBetweenAds - 1; i++)
            {
                policy.RegisterRunCompleted();
                Assert.That(policy.CanShow(GameState.GameOver), Is.False);
            }

            policy.RegisterRunCompleted();
            Assert.That(policy.CanShow(GameState.GameOver), Is.True);
        }

        [Test]
        public void RemoveAdsSuppressesInterstitialsEntirely()
        {
            var policy = PolicyAfterRuns(50);
            policy.RemoveAdsPurchased = true;

            Assert.That(policy.CanShow(GameState.GameOver), Is.False);
        }
    }

    [TestFixture]
    public class AdManagerTests
    {
        private static (AdManager ads, MockAdProvider provider) NewManager()
        {
            var provider = new MockAdProvider();
            var ads = new AdManager(provider);
            ads.Preload();
            return (ads, provider);
        }

        [Test]
        public void WatchingToCompletionEarnsTheReward()
        {
            var (ads, _) = NewManager();
            RewardedOutcome? outcome = null;

            ads.ShowRewarded(RewardedPlacement.Revive, o => outcome = o);

            Assert.That(outcome, Is.EqualTo(RewardedOutcome.Earned));
        }

        [Test]
        public void DismissingEarlyEarnsNothingButIsNotAFailure()
        {
            var (ads, provider) = NewManager();
            provider.NextOutcome = RewardedOutcome.Dismissed;
            RewardedOutcome? outcome = null;

            ads.ShowRewarded(RewardedPlacement.DoubleCoins, o => outcome = o);

            Assert.That(outcome, Is.EqualTo(RewardedOutcome.Dismissed));
        }

        [Test]
        public void NoAdAvailableStillInvokesTheCallback()
        {
            // Section 55: the run must continue. A caller left waiting on a callback that never
            // arrives is a soft-lock, which is worse than no ad at all.
            var provider = new MockAdProvider { AutoLoad = false };
            var ads = new AdManager(provider);
            RewardedOutcome? outcome = null;

            ads.ShowRewarded(RewardedPlacement.Revive, o => outcome = o);

            Assert.That(outcome, Is.EqualTo(RewardedOutcome.Unavailable));
        }

        [Test]
        public void AThrowingAdSdkDoesNotEscapeToGameplay()
        {
            var provider = new MockAdProvider { ThrowOnShow = true };
            var ads = new AdManager(provider);
            ads.Preload();
            RewardedOutcome? outcome = null;

            Assert.DoesNotThrow(() => ads.ShowRewarded(RewardedPlacement.Revive, o => outcome = o));
            Assert.That(outcome, Is.EqualTo(RewardedOutcome.Failed));
        }

        [Test]
        public void ADuplicateProviderCallbackGrantsTheRewardOnlyOnce()
        {
            // Ad SDKs really do double-fire. Here that would mean two revives from one video.
            var provider = new MockAdProvider { InvokeCallbackTwice = true };
            var ads = new AdManager(provider);
            ads.Preload();
            int callbacks = 0;

            ads.ShowRewarded(RewardedPlacement.Revive, _ => callbacks++);

            Assert.That(callbacks, Is.EqualTo(1));
        }

        [Test]
        public void RewardEarnedIsReportedForAnalytics()
        {
            var (ads, _) = NewManager();
            var events = new List<AdEvent>();
            ads.RewardedEvent += (e, _) => events.Add(e);

            ads.ShowRewarded(RewardedPlacement.BonusCoins, _ => { });

            Assert.That(events, Does.Contain(AdEvent.Shown));
            Assert.That(events, Does.Contain(AdEvent.RewardEarned));
            Assert.That(events, Does.Contain(AdEvent.Closed));
        }

        [Test]
        public void TheNextAdIsQueuedAfterOneIsConsumed()
        {
            var (ads, provider) = NewManager();

            ads.ShowRewarded(RewardedPlacement.Revive, _ => { });

            Assert.That(provider.IsRewardedReady(RewardedPlacement.Revive), Is.True,
                "The following offer would be unavailable — section 23 asks for appropriate preloading.");
        }

        [Test]
        public void RewardedStaysAvailableAfterRemoveAds()
        {
            // The shop promises "No interstitials. Rewards stay optional." Taking the rewarded
            // path away from a paying player would be removing something they can still want.
            var (ads, _) = NewManager();
            ads.RemoveAdsPurchased = true;
            RewardedOutcome? outcome = null;

            ads.ShowRewarded(RewardedPlacement.DoubleCoins, o => outcome = o);

            Assert.That(outcome, Is.EqualTo(RewardedOutcome.Earned));
            Assert.That(ads.TryShowInterstitial(GameState.GameOver), Is.False);
        }

        [Test]
        public void InterstitialIsNotShownWhenPolicyForbidsIt()
        {
            var (ads, provider) = NewManager();

            Assert.That(ads.TryShowInterstitial(GameState.GameOver), Is.False);
            Assert.That(provider.InterstitialShowCount, Is.Zero);
        }

        [Test]
        public void InterstitialShowsOnceThePolicyAllowsIt()
        {
            var (ads, provider) = NewManager();
            for (int i = 0; i < 10; i++) ads.RegisterRunCompleted();
            ads.Tick(600f);

            Assert.That(ads.TryShowInterstitial(GameState.GameOver), Is.True);
            Assert.That(provider.InterstitialShowCount, Is.EqualTo(1));
        }

        [Test]
        public void AnUnloadedInterstitialIsSkippedRatherThanBlocking()
        {
            var (ads, provider) = NewManager();
            for (int i = 0; i < 10; i++) ads.RegisterRunCompleted();
            ads.Tick(600f);
            provider.InterstitialLoaded = false;
            provider.AutoLoad = false;

            Assert.That(ads.TryShowInterstitial(GameState.GameOver), Is.False,
                "The player should proceed immediately, not wait on an ad that isn't there.");
        }
    }
}
