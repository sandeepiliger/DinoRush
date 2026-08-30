using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // The long-lived systems from section 33, constructed once at boot in the order section 53
    // specifies: save first, then analytics, then ads, then everything that depends on them.
    //
    // Grouped into one object because the alternative is a constructor with ten parameters that
    // every future system extends by one. Section 33 also warns against one giant GameManager —
    // this deliberately holds references and wires startup, and contains no game logic itself.
    public sealed class GameServices
    {
        public SaveService Save { get; }
        public AnalyticsManager Analytics { get; }
        public AdManager Ads { get; }
        public IapManager Iap { get; }
        public CollectionManager Collection { get; }
        public MissionTracker Missions { get; }
        public GameConfig Config { get; }

        public GameServices()
        {
            // 1. Save — everything else reads progression state, so it has to exist first.
            Save = new SaveService();

            // 2. Analytics — wanted early enough to record the session and any first-open.
            Analytics = new AnalyticsManager(new MockAnalyticsProvider());
            Analytics.BeginSession();

            // 3. Config — local only for now (section 52); a remote provider swaps in here.
            Config = new GameConfig(new LocalGameConfigProvider());

            // 4. Ads. Still the mock provider: Google Mobile Ads needs an AdMob app ID and test
            //    unit IDs that don't exist yet, and section 23 forbids anything but test IDs in
            //    development. Swapping in the real provider is a one-line change here.
            Ads = new AdManager(new MockAdProvider());
            Ads.RemoveAdsPurchased = Save.Data.RemoveAdsPurchased;
            Ads.Preload();

            // 5. Progression.
            Collection = new CollectionManager(Save.Data);
            Iap = new IapManager(new MockIapProvider(), Save.Data, Collection);

            Missions = new MissionTracker();
            DailyMissionRotation.EnsureCurrent(Save.Data, GameClock.TodayIndexUtc, Missions);

            // Purchases can change ad behaviour, so keep the two in step rather than reading
            // the save again at every ad site.
            Iap.PurchaseCompleted += (_, result) =>
            {
                if (result == PurchaseResult.Purchased)
                {
                    Ads.RemoveAdsPurchased = Save.Data.RemoveAdsPurchased;
                    Save.Save();
                }
            };

            // Section 25's ad event list, routed to analytics without gameplay code knowing
            // either subsystem exists.
            Ads.RewardedEvent += (adEvent, placement) =>
            {
                if (adEvent == AdEvent.Shown)
                    Analytics.Track(AnalyticsEvent.RewardedAdOffered, "placement", placement.ToString());
                else if (adEvent == AdEvent.RewardEarned)
                    Analytics.Track(AnalyticsEvent.RewardedAdCompleted, "placement", placement.ToString());
            };
            Ads.InterstitialEvent += adEvent =>
            {
                if (adEvent == AdEvent.Shown) Analytics.Track(AnalyticsEvent.InterstitialShown);
            };

            if (Save.LastOutcome == SaveLoadOutcome.StartedFresh && Save.Data.BestScore == 0)
                Analytics.Track(AnalyticsEvent.FirstOpen);

            Debug.Log($"[DinoRush] Services ready. Save: {Save.LastOutcome}. " +
                      $"Coins {Save.Data.Coins}, best {Save.Data.BestScore}, " +
                      $"{Collection.Unlocked.Count} dinosaur(s) unlocked.");
        }

        public void Shutdown()
        {
            Analytics.EndSession();
            Save.Save();
        }
    }
}
