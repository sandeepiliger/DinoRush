using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // Section 52: "Never hardcode balancing values throughout scripts." Every tunable the spec
    // lists lives behind a key here, so the local defaults can later be overridden by a remote
    // config service without touching a line of gameplay code.
    public static class ConfigKeys
    {
        public const string StartingSpeed = "starting_speed";
        public const string DifficultyCurveScale = "difficulty_curve_scale";
        public const string CoinRewardMultiplier = "coin_reward_multiplier";
        public const string MissionRewardMultiplier = "mission_reward_multiplier";
        public const string InterstitialRunsBetween = "interstitial_runs_between";
        public const string InterstitialMinimumRuns = "interstitial_minimum_runs";
        public const string ReviveEnabled = "revive_enabled";
        public const string DoubleCoinsEnabled = "double_coins_enabled";
        public const string DailyChallengeEnabled = "daily_challenge_enabled";
        public const string ShopEnabled = "shop_enabled";
    }

    public interface IGameConfigProvider
    {
        bool TryGetFloat(string key, out float value);
        bool TryGetInt(string key, out int value);
        bool TryGetBool(string key, out bool value);
    }

    // Reads a tunable, falling back to a compiled-in default whenever the provider has nothing
    // to say. That fallback is the important part: section 54 requires the game work offline,
    // and section 55 requires a missing service never break gameplay — so a config lookup can
    // never fail, only return the default.
    public sealed class GameConfig
    {
        private readonly IGameConfigProvider _provider;

        public GameConfig(IGameConfigProvider provider = null)
        {
            // Null is a legitimate state, not an error: it is exactly what "no remote config
            // yet" looks like, and everything must still work.
            _provider = provider;
        }

        public float GetFloat(string key, float fallback)
        {
            try
            {
                if (_provider != null && _provider.TryGetFloat(key, out float value)) return value;
            }
            catch (Exception)
            {
                // A misbehaving remote provider must not be able to change the game's balance
                // by throwing — fall through to the known-good default.
            }
            return fallback;
        }

        public int GetInt(string key, int fallback)
        {
            try
            {
                if (_provider != null && _provider.TryGetInt(key, out int value)) return value;
            }
            catch (Exception) { }
            return fallback;
        }

        public bool GetBool(string key, bool fallback)
        {
            try
            {
                if (_provider != null && _provider.TryGetBool(key, out bool value)) return value;
            }
            catch (Exception) { }
            return fallback;
        }
    }

    // The initial implementation section 52 asks for: local values only. A remote provider
    // implementing the same interface can be dropped in later without any caller changing.
    public sealed class LocalGameConfigProvider : IGameConfigProvider
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);

        public LocalGameConfigProvider()
        {
            // Defaults deliberately mirror the constants the systems already use, so this
            // provider changes no behaviour until someone actually overrides a value.
            Set(ConfigKeys.StartingSpeed, 8f);
            Set(ConfigKeys.DifficultyCurveScale, 1f);
            Set(ConfigKeys.CoinRewardMultiplier, 1f);
            Set(ConfigKeys.MissionRewardMultiplier, 1f);
            Set(ConfigKeys.InterstitialRunsBetween, InterstitialPolicy.RunsBetweenAds);
            Set(ConfigKeys.InterstitialMinimumRuns, InterstitialPolicy.MinimumRunsBeforeFirstAd);
            Set(ConfigKeys.ReviveEnabled, true);
            Set(ConfigKeys.DoubleCoinsEnabled, true);
            Set(ConfigKeys.DailyChallengeEnabled, true);
            Set(ConfigKeys.ShopEnabled, true);
        }

        public void Set(string key, object value) => _values[key] = value;

        public bool TryGetFloat(string key, out float value)
        {
            if (_values.TryGetValue(key, out var stored))
            {
                switch (stored)
                {
                    case float f: value = f; return true;
                    case int i: value = i; return true;  // an int default is a valid float
                }
            }
            value = 0f;
            return false;
        }

        public bool TryGetInt(string key, out int value)
        {
            if (_values.TryGetValue(key, out var stored) && stored is int i)
            {
                value = i;
                return true;
            }
            value = 0;
            return false;
        }

        public bool TryGetBool(string key, out bool value)
        {
            if (_values.TryGetValue(key, out var stored) && stored is bool b)
            {
                value = b;
                return true;
            }
            value = false;
            return false;
        }
    }
}
