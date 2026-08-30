using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // The stand-in required by section 70: when a real service isn't wired up, provide a clean
    // abstraction and a temporary implementation rather than inventing something that breaks
    // later. Google Mobile Ads is not in this project yet (adding it means an SDK, an app ID
    // and test unit IDs — section 23 is emphatic that only test IDs are used in development),
    // so this keeps every dependent system buildable and testable in the meantime.
    //
    // It is also the only ad provider that can run in CI, which is why the failure modes are
    // configurable: "ad unavailable" and "SDK throws" are states the game must survive
    // (section 55), and they are far easier to exercise here than against a live network.
    public sealed class MockAdProvider : IAdProvider
    {
        private readonly HashSet<RewardedPlacement> _loaded = new HashSet<RewardedPlacement>();

        // Test/dev switches. Defaults model the happy path.
        public bool AutoLoad { get; set; } = true;
        public RewardedOutcome NextOutcome { get; set; } = RewardedOutcome.Earned;
        public bool ThrowOnShow { get; set; }
        public bool InvokeCallbackTwice { get; set; }
        public bool InterstitialLoaded { get; set; } = true;

        public int RewardedShowCount { get; private set; }
        public int InterstitialShowCount { get; private set; }
        public int LoadRequestCount { get; private set; }

        public bool IsRewardedReady(RewardedPlacement placement) => _loaded.Contains(placement);

        public void LoadRewarded(RewardedPlacement placement)
        {
            LoadRequestCount++;
            if (AutoLoad) _loaded.Add(placement);
        }

        public void ShowRewarded(RewardedPlacement placement, Action<RewardedOutcome> onComplete)
        {
            if (ThrowOnShow) throw new InvalidOperationException("simulated ad SDK failure");

            RewardedShowCount++;
            _loaded.Remove(placement); // a shown ad is consumed, exactly as a real one is

            onComplete(NextOutcome);
            if (InvokeCallbackTwice) onComplete(NextOutcome);
        }

        public bool IsInterstitialReady => InterstitialLoaded;

        public void LoadInterstitial()
        {
            LoadRequestCount++;
            if (AutoLoad) InterstitialLoaded = true;
        }

        public void ShowInterstitial(Action onClosed)
        {
            if (ThrowOnShow) throw new InvalidOperationException("simulated ad SDK failure");

            InterstitialShowCount++;
            InterstitialLoaded = false;
            onClosed();
        }
    }
}
