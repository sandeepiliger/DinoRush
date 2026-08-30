using System;

namespace DinoRush.Core
{
    // The single entry point gameplay uses for advertising — section 25:
    //     AdManager.ShowRewarded(RewardType.Revive)
    // not
    //     RewardedAd.Show(...)
    //
    // Keeping the network behind this class is what makes monetisation replaceable, and it is
    // also what makes section 55 ("never allow external services to crash gameplay") tractable:
    // every failure path is handled in one place rather than at each call site.
    public sealed class AdManager
    {
        private readonly IAdProvider _provider;
        private readonly InterstitialPolicy _policy;

        public event Action<AdEvent, RewardedPlacement> RewardedEvent;
        public event Action<AdEvent> InterstitialEvent;

        public AdManager(IAdProvider provider, InterstitialPolicy policy = null)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _policy = policy ?? new InterstitialPolicy();
        }

        public InterstitialPolicy Policy => _policy;

        public bool RemoveAdsPurchased
        {
            get => _policy.RemoveAdsPurchased;
            // Rewarded ads stay available after Remove Ads — the design's shop says exactly
            // that ("No interstitials. Rewards stay optional."). Removing the player's ability
            // to opt into a reward would be taking something away for paying.
            set => _policy.RemoveAdsPurchased = value;
        }

        // Whether the offer should even be presented. Section 23: never force a rewarded ad,
        // and don't offer one that isn't there to give — an offer that fails on tap is worse
        // than no offer.
        public bool IsRewardedAvailable(RewardedPlacement placement) => _provider.IsRewardedReady(placement);

        public void Preload()
        {
            // Section 23: preload rewarded appropriately. Revive is preloaded above all because
            // it is offered under a countdown — a load started at death would miss the window.
            foreach (RewardedPlacement placement in Enum.GetValues(typeof(RewardedPlacement)))
            {
                _provider.LoadRewarded(placement);
                RewardedEvent?.Invoke(AdEvent.Requested, placement);
            }

            if (!_policy.RemoveAdsPurchased) _provider.LoadInterstitial();
        }

        // onOutcome always fires, exactly once, whatever happens — including when no ad is
        // available. A caller that only handled success would leave the player stuck on a
        // spinner when the network is down.
        public void ShowRewarded(RewardedPlacement placement, Action<RewardedOutcome> onOutcome)
        {
            if (onOutcome == null) throw new ArgumentNullException(nameof(onOutcome));

            if (!_provider.IsRewardedReady(placement))
            {
                RewardedEvent?.Invoke(AdEvent.FailedToLoad, placement);
                onOutcome(RewardedOutcome.Unavailable);
                return;
            }

            RewardedEvent?.Invoke(AdEvent.Shown, placement);
            _policy.RegisterRewardedShown();

            bool handled = false;
            try
            {
                _provider.ShowRewarded(placement, outcome =>
                {
                    // Guards against a provider that invokes its callback more than once — a
                    // real hazard with ad SDKs, and here it would mean granting a revive twice.
                    if (handled) return;
                    handled = true;

                    if (outcome == RewardedOutcome.Earned)
                        RewardedEvent?.Invoke(AdEvent.RewardEarned, placement);

                    RewardedEvent?.Invoke(AdEvent.Closed, placement);

                    // Immediately queue the next one so the following offer is ready.
                    _provider.LoadRewarded(placement);
                    onOutcome(outcome);
                });
            }
            catch (Exception)
            {
                // A throwing ad SDK must not take the run with it (section 55).
                if (!handled)
                {
                    handled = true;
                    onOutcome(RewardedOutcome.Failed);
                }
            }
        }

        // Returns whether an ad was actually shown, so the caller can proceed immediately when
        // it wasn't rather than waiting on a callback that will never come.
        public bool TryShowInterstitial(GameState state, Action onClosed = null)
        {
            if (!_policy.CanShow(state)) return false;
            if (!_provider.IsInterstitialReady)
            {
                _provider.LoadInterstitial();
                return false;
            }

            _policy.RegisterInterstitialShown();
            InterstitialEvent?.Invoke(AdEvent.Shown);

            try
            {
                _provider.ShowInterstitial(() =>
                {
                    InterstitialEvent?.Invoke(AdEvent.Closed);
                    _provider.LoadInterstitial();
                    onClosed?.Invoke();
                });
                return true;
            }
            catch (Exception)
            {
                InterstitialEvent?.Invoke(AdEvent.FailedToLoad);
                onClosed?.Invoke();
                return false;
            }
        }

        public void RegisterRunCompleted() => _policy.RegisterRunCompleted();
        public void Tick(float deltaSeconds) => _policy.Tick(deltaSeconds);
    }
}
