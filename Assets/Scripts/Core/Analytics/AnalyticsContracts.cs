using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // Section 28's event list, as an enum rather than loose strings. Event names become a
    // contract with whatever dashboard consumes them — renaming one silently splits a metric
    // in two — so they are defined once and mapped to wire names in exactly one place.
    public enum AnalyticsEvent
    {
        // Acquisition
        FirstOpen,
        TutorialStarted,
        TutorialCompleted,

        // Gameplay
        RunStarted,
        RunCompleted,
        PlayerDied,
        DistanceReached,
        ObstacleHit,
        BiomeEntered,

        // Progression
        DinosaurUnlocked,
        DinosaurSelected,
        MissionCompleted,
        DailyRewardClaimed,

        // Monetisation
        RewardedAdOffered,
        RewardedAdCompleted,
        InterstitialShown,
        PurchaseStarted,
        PurchaseCompleted,

        // Retention
        SessionStarted,
        SessionEnded,
        DayReturn,
    }

    // The seam a real SDK (Firebase, GameAnalytics, Unity Analytics) plugs into. Nothing above
    // this line knows which vendor is in use — section 28: "Never put analytics SDK-specific
    // calls throughout the game."
    public interface IAnalyticsProvider
    {
        void Track(string eventName, IReadOnlyDictionary<string, object> parameters);
        void SetUserProperty(string key, string value);
    }

    public static class AnalyticsEventNames
    {
        // snake_case wire names, matching section 28's spelling exactly. Kept separate from the
        // enum so the enum can be renamed for readability without breaking historical data.
        private static readonly Dictionary<AnalyticsEvent, string> Names = new Dictionary<AnalyticsEvent, string>
        {
            [AnalyticsEvent.FirstOpen] = "first_open",
            [AnalyticsEvent.TutorialStarted] = "tutorial_started",
            [AnalyticsEvent.TutorialCompleted] = "tutorial_completed",
            [AnalyticsEvent.RunStarted] = "run_started",
            [AnalyticsEvent.RunCompleted] = "run_completed",
            [AnalyticsEvent.PlayerDied] = "player_died",
            [AnalyticsEvent.DistanceReached] = "distance_reached",
            [AnalyticsEvent.ObstacleHit] = "obstacle_hit",
            [AnalyticsEvent.BiomeEntered] = "biome_entered",
            [AnalyticsEvent.DinosaurUnlocked] = "dinosaur_unlocked",
            [AnalyticsEvent.DinosaurSelected] = "dinosaur_selected",
            [AnalyticsEvent.MissionCompleted] = "mission_completed",
            [AnalyticsEvent.DailyRewardClaimed] = "daily_reward_claimed",
            [AnalyticsEvent.RewardedAdOffered] = "rewarded_ad_offered",
            [AnalyticsEvent.RewardedAdCompleted] = "rewarded_ad_completed",
            [AnalyticsEvent.InterstitialShown] = "interstitial_shown",
            [AnalyticsEvent.PurchaseStarted] = "purchase_started",
            [AnalyticsEvent.PurchaseCompleted] = "purchase_completed",
            [AnalyticsEvent.SessionStarted] = "session_started",
            [AnalyticsEvent.SessionEnded] = "session_ended",
            [AnalyticsEvent.DayReturn] = "day_return",
        };

        public static string Of(AnalyticsEvent value) =>
            Names.TryGetValue(value, out var name)
                ? name
                : throw new ArgumentOutOfRangeException(nameof(value), $"No wire name registered for {value}.");
    }
}
