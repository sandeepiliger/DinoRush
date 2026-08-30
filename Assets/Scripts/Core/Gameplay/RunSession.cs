using System;

namespace DinoRush.Core
{
    // One playthrough's live state: elapsed time, distance, coins, and whether the player is
    // still alive.
    //
    // This is deliberately engine-free (docs/DECISIONS.md D9). M4's PlayerController becomes a
    // thin Unity shell that feeds Time.deltaTime into Tick() and reads CurrentSpeed back out,
    // which keeps the run's actual rules — speed escalation, scoring, the one-revive-per-run
    // limit — unit-testable without pressing Play.
    public sealed class RunSession
    {
        private readonly RunGenerationConfig _config;

        public int Seed { get; }
        public float ElapsedSeconds { get; private set; }
        public float DistanceMeters { get; private set; }
        public int CoinsCollected { get; private set; }
        public bool IsAlive { get; private set; } = true;
        public bool HasUsedRevive { get; private set; }

        public RunSession(RunGenerationConfig config, int seed)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Seed = seed;
        }

        // Time drives escalation; distance is derived from it. See docs/DECISIONS.md D4 —
        // distance is a display value, never the authority.
        public DifficultyTier CurrentTier => _config.Difficulty.GetTierWindow(ElapsedSeconds).Tier;

        public float CurrentSpeed =>
            _config.BaseRunSpeedMetersPerSecond * _config.Difficulty.GetTierWindow(ElapsedSeconds).RunSpeedMultiplier;

        public int Score => ScoreCalculator.CalculateScore(DistanceMeters, CoinsCollected);

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (!IsAlive) return; // a dead run accumulates nothing until revived or ended

            // Speed is sampled before advancing the clock so a single Tick can't straddle a
            // tier boundary and retroactively apply the faster tier to time already elapsed.
            float speed = CurrentSpeed;
            ElapsedSeconds += deltaSeconds;
            DistanceMeters += speed * deltaSeconds;
        }

        public void CollectCoin(int amount = 1)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!IsAlive) return;
            CoinsCollected += amount;
        }

        public void Die()
        {
            IsAlive = false;
        }

        // Returns false when a revive isn't available, rather than throwing: the caller is a
        // UI flow reacting to an ad result, and "already used" is an expected outcome there,
        // not a programming error. One revive per run — D3.
        public bool TryRevive()
        {
            if (IsAlive || HasUsedRevive) return false;

            HasUsedRevive = true;
            IsAlive = true;
            return true;
        }
    }
}
