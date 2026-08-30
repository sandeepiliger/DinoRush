using System;

namespace DinoRush.Core
{
    // Decides whether an interstitial may be shown right now. Pure policy, no SDK — so every
    // rule section 24 lays down is unit-testable instead of being an emergent property of
    // whatever the ad callback happens to do.
    //
    // Section 24's prohibitions, each enforced below:
    //   - never immediately on app launch
    //   - never during active gameplay
    //   - never immediately after a rewarded ad
    //   - never interrupting a critical interaction
    // plus a frequency cap, and section 27's principle that a paying player stops seeing them.
    public sealed class InterstitialPolicy
    {
        // Deliberately generous. Section 27: the game must be enjoyable without paying, and a
        // new player who is interrupted twice in their first few minutes simply leaves. These
        // are the values GameConfigProvider will make remotely tunable (section 52) — the point
        // of routing them through one class is that tuning never means touching gameplay code.
        public const int MinimumRunsBeforeFirstAd = 4;
        public const int RunsBetweenAds = 3;
        public const float MinimumSecondsAfterRewarded = 30f;

        private int _runsCompleted;

        // Nullable rather than a sentinel like int.MinValue: `_runsCompleted - int.MinValue`
        // overflows to a negative number, which silently fails the frequency check and
        // suppresses every interstitial forever. Caught by FrequencyCapSpacesAdsOut.
        private int? _runAtLastInterstitial;
        private float _secondsSinceRewarded = float.MaxValue;

        public bool RemoveAdsPurchased { get; set; }

        public int RunsCompleted => _runsCompleted;

        public void RegisterRunCompleted() => _runsCompleted++;

        public void RegisterRewardedShown() => _secondsSinceRewarded = 0f;

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (_secondsSinceRewarded < float.MaxValue) _secondsSinceRewarded += deltaSeconds;
        }

        // `state` is passed rather than assumed so the "never during gameplay" rule is checked
        // against reality instead of trusting the caller to only ask at the right moment.
        public bool CanShow(GameState state)
        {
            if (RemoveAdsPurchased) return false;

            // Only ever between runs. GameOver is the one non-disruptive moment in the loop.
            if (state != GameState.GameOver) return false;

            // Never on launch, and never to a player still learning the game.
            if (_runsCompleted < MinimumRunsBeforeFirstAd) return false;

            // Never straight after a rewarded ad — watching one and being served another
            // immediately is the fastest way to make the rewarded offer feel like a trap.
            if (_secondsSinceRewarded < MinimumSecondsAfterRewarded) return false;

            if (_runAtLastInterstitial is not int lastRun) return true; // none shown yet
            return _runsCompleted - lastRun >= RunsBetweenAds;
        }

        public void RegisterInterstitialShown() => _runAtLastInterstitial = _runsCompleted;
    }
}
