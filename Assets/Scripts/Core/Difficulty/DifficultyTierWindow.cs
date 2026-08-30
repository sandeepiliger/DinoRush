using System;

namespace DinoRush.Core
{
    public readonly struct DifficultyTierWindow
    {
        public DifficultyTier Tier { get; }
        public float StartTimeSeconds { get; }
        public float RunSpeedMultiplier { get; }

        public DifficultyTierWindow(DifficultyTier tier, float startTimeSeconds, float runSpeedMultiplier)
        {
            if (startTimeSeconds < 0) throw new ArgumentOutOfRangeException(nameof(startTimeSeconds));
            if (runSpeedMultiplier <= 0) throw new ArgumentOutOfRangeException(nameof(runSpeedMultiplier));

            Tier = tier;
            StartTimeSeconds = startTimeSeconds;
            RunSpeedMultiplier = runSpeedMultiplier;
        }
    }
}
