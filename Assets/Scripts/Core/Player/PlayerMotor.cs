using System;

namespace DinoRush.Core
{
    public enum PlayerIntent
    {
        None,
        Jump,
        Duck,
    }

    public enum PlayerStance
    {
        Running,
        Airborne,
        Ducking,
    }

    // The player's vertical motion, as pure math. Deliberately not a Rigidbody: an endless
    // runner's jump needs to be exactly reproducible frame-for-frame (a given input at a given
    // speed must always clear the same obstacle), and Unity's physics solver offers no such
    // guarantee across framerates or platforms. Keeping it here also makes the whole thing
    // unit-testable without pressing Play — docs/DECISIONS.md D9.
    public sealed class PlayerMotor
    {
        private readonly PlayerMotorConfig _config;
        private float _verticalVelocity;
        private float _duckRemainingSeconds;

        public PlayerMotor(PlayerMotorConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public float FeetHeightMeters { get; private set; }
        public PlayerStance Stance { get; private set; } = PlayerStance.Running;

        // Exposed for presentation only. The animator needs to know whether a jump is on its way
        // up or on its way down — a dinosaur tucks its legs while rising and reaches with them
        // while falling, and the two look nothing alike.
        public float VerticalVelocityMetersPerSecond => _verticalVelocity;

        public float CurrentHeightMeters =>
            Stance == PlayerStance.Ducking ? _config.DuckingHeightMeters : _config.StandingHeightMeters;

        public float HeadHeightMeters => FeetHeightMeters + CurrentHeightMeters;

        public void Tick(float deltaSeconds, PlayerIntent intent)
        {
            if (deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            ApplyIntent(intent);

            if (Stance == PlayerStance.Airborne)
            {
                // Semi-implicit Euler: velocity is integrated before position. Plain Euler
                // makes apex height drift with framerate, which would let a low-FPS device
                // fail a jump a high-FPS device clears.
                _verticalVelocity -= _config.GravityMetersPerSecondSquared * deltaSeconds;
                FeetHeightMeters += _verticalVelocity * deltaSeconds;

                if (FeetHeightMeters <= 0f)
                {
                    FeetHeightMeters = 0f;
                    _verticalVelocity = 0f;
                    Stance = PlayerStance.Running;
                }
            }
            else if (Stance == PlayerStance.Ducking)
            {
                _duckRemainingSeconds -= deltaSeconds;
                if (_duckRemainingSeconds <= 0f)
                {
                    _duckRemainingSeconds = 0f;
                    Stance = PlayerStance.Running;
                }
            }
        }

        private void ApplyIntent(PlayerIntent intent)
        {
            switch (intent)
            {
                case PlayerIntent.Jump:
                    // Jumping cancels a duck, so a mistimed duck never traps the player on the
                    // ground in front of a jump obstacle. No double-jump in the MVP (section 14).
                    if (Stance != PlayerStance.Airborne)
                    {
                        Stance = PlayerStance.Airborne;
                        _verticalVelocity = _config.JumpVelocityMetersPerSecond;
                        _duckRemainingSeconds = 0f;
                    }
                    break;

                case PlayerIntent.Duck:
                    // Ducking is ignored mid-air: allowing it would silently shrink the
                    // hitbox during a jump and make duck obstacles passable by jumping,
                    // collapsing the two mechanics into one.
                    if (Stance != PlayerStance.Airborne)
                    {
                        Stance = PlayerStance.Ducking;
                        _duckRemainingSeconds = _config.DuckDurationSeconds;
                    }
                    break;
            }
        }

        public void Reset()
        {
            FeetHeightMeters = 0f;
            _verticalVelocity = 0f;
            _duckRemainingSeconds = 0f;
            Stance = PlayerStance.Running;
        }
    }
}
