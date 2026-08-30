using System;

namespace DinoRush.Core
{
    // Every rewarded placement in the design. Section 23 requires the reward be clear before
    // the player chooses to watch, so a placement carries its own description rather than the
    // UI inventing one.
    public enum RewardedPlacement
    {
        Revive,
        DoubleCoins,
        BonusCoins,
        DailyRewardMultiplier,
        Boost,
    }

    public enum RewardedOutcome
    {
        Earned,        // watched to completion — grant the reward
        Dismissed,     // closed early — no reward, no penalty
        Unavailable,   // nothing loaded, or the SDK failed
        Failed,        // show attempted and errored
    }

    // Section 25's event list. Kept as an enum so AnalyticsManager can log every one without
    // ad-network vocabulary leaking into gameplay code.
    public enum AdEvent
    {
        Requested,
        Loaded,
        FailedToLoad,
        Shown,
        Closed,
        RewardEarned,
    }

    // The seam the real Google Mobile Ads SDK plugs into. Nothing above this line knows that
    // AdMob exists — section 25: gameplay calls AdManager.ShowRewarded(...), never
    // RewardedAd.Show(...), so the network can be swapped without touching the game.
    public interface IAdProvider
    {
        bool IsRewardedReady(RewardedPlacement placement);
        void LoadRewarded(RewardedPlacement placement);
        void ShowRewarded(RewardedPlacement placement, Action<RewardedOutcome> onComplete);

        bool IsInterstitialReady { get; }
        void LoadInterstitial();
        void ShowInterstitial(Action onClosed);
    }

    public static class RewardedPlacementInfo
    {
        // Shown to the player before they opt in. Section 23: rewards must be clearly
        // communicated, and section 27 wants the offer to read as "would you like this?"
        public static string Describe(RewardedPlacement placement)
        {
            switch (placement)
            {
                case RewardedPlacement.Revive: return "Watch a short video to get back up where you fell.";
                case RewardedPlacement.DoubleCoins: return "Watch a short video to double this run's coins.";
                case RewardedPlacement.BonusCoins: return "Watch a short video for bonus coins.";
                case RewardedPlacement.DailyRewardMultiplier: return "Watch a short video to double today's reward.";
                case RewardedPlacement.Boost: return "Watch a short video to start with a boost.";
                default: throw new ArgumentOutOfRangeException(nameof(placement));
            }
        }
    }
}
