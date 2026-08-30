using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class PlayerMotorTests
    {
        private const float Step = 1f / 60f;

        private static PlayerMotor NewMotor() => new PlayerMotor(PlayerMotorConfig.CreateDefault());

        private static void Advance(PlayerMotor motor, float seconds, PlayerIntent first = PlayerIntent.None)
        {
            motor.Tick(Step, first);
            for (float t = Step; t < seconds; t += Step)
                motor.Tick(Step, PlayerIntent.None);
        }

        [Test]
        public void StartsGroundedAndStanding()
        {
            var motor = NewMotor();
            Assert.That(motor.Stance, Is.EqualTo(PlayerStance.Running));
            Assert.That(motor.FeetHeightMeters, Is.Zero);
            Assert.That(motor.HeadHeightMeters, Is.EqualTo(PlayerMotorConfig.CreateDefault().StandingHeightMeters));
        }

        [Test]
        public void JumpLeavesTheGroundAndReturnsToIt()
        {
            var motor = NewMotor();
            var config = PlayerMotorConfig.CreateDefault();

            motor.Tick(Step, PlayerIntent.Jump);
            Assert.That(motor.Stance, Is.EqualTo(PlayerStance.Airborne));

            Advance(motor, config.JumpAirtimeSeconds + 0.2f);

            Assert.That(motor.Stance, Is.EqualTo(PlayerStance.Running));
            Assert.That(motor.FeetHeightMeters, Is.Zero);
        }

        [Test]
        public void JumpReachesRoughlyTheAnalyticApex()
        {
            var motor = NewMotor();
            var config = PlayerMotorConfig.CreateDefault();

            float peak = 0f;
            motor.Tick(Step, PlayerIntent.Jump);
            for (float t = 0; t < config.JumpAirtimeSeconds; t += Step)
            {
                motor.Tick(Step, PlayerIntent.None);
                if (motor.FeetHeightMeters > peak) peak = motor.FeetHeightMeters;
            }

            // Discrete integration won't hit the closed-form apex exactly; a 10% band is tight
            // enough to catch a real regression in the integrator.
            Assert.That(peak, Is.EqualTo(config.JumpApexMeters).Within(config.JumpApexMeters * 0.1f));
        }

        [Test]
        public void DuckShrinksSilhouetteThenRecovers()
        {
            var motor = NewMotor();
            var config = PlayerMotorConfig.CreateDefault();

            motor.Tick(Step, PlayerIntent.Duck);
            Assert.That(motor.Stance, Is.EqualTo(PlayerStance.Ducking));
            Assert.That(motor.CurrentHeightMeters, Is.EqualTo(config.DuckingHeightMeters));

            Advance(motor, config.DuckDurationSeconds + 0.1f);

            Assert.That(motor.Stance, Is.EqualTo(PlayerStance.Running));
            Assert.That(motor.CurrentHeightMeters, Is.EqualTo(config.StandingHeightMeters));
        }

        [Test]
        public void DuckIsIgnoredMidAir()
        {
            var motor = NewMotor();

            motor.Tick(Step, PlayerIntent.Jump);
            motor.Tick(Step, PlayerIntent.Duck);

            Assert.That(motor.Stance, Is.EqualTo(PlayerStance.Airborne),
                "Ducking in mid-air would shrink the hitbox during a jump and let a jump clear duck obstacles too.");
        }

        [Test]
        public void JumpCancelsAnActiveDuck()
        {
            var motor = NewMotor();

            motor.Tick(Step, PlayerIntent.Duck);
            motor.Tick(Step, PlayerIntent.Jump);

            Assert.That(motor.Stance, Is.EqualTo(PlayerStance.Airborne));
        }

        [Test]
        public void NoDoubleJump()
        {
            var motor = NewMotor();

            motor.Tick(Step, PlayerIntent.Jump);
            Advance(motor, 0.15f);
            float heightBefore = motor.FeetHeightMeters;
            motor.Tick(Step, PlayerIntent.Jump); // second press mid-air

            // A double jump would re-apply full upward velocity; without it the player keeps
            // decelerating under gravity. Section 14 lists double jump as a future ability.
            float expectedIfDoubleJumped = heightBefore + PlayerMotorConfig.CreateDefault().JumpVelocityMetersPerSecond * Step;
            Assert.That(motor.FeetHeightMeters, Is.LessThan(expectedIfDoubleJumped));
        }

        [Test]
        public void ApexIsFramerateIndependent()
        {
            // Same jump, wildly different step sizes: a low-FPS device must not fail a jump a
            // high-FPS device clears.
            float PeakWithStep(float step)
            {
                var motor = NewMotor();
                var config = PlayerMotorConfig.CreateDefault();
                float peak = 0f;
                motor.Tick(step, PlayerIntent.Jump);
                for (float t = 0; t < config.JumpAirtimeSeconds + step; t += step)
                {
                    motor.Tick(step, PlayerIntent.None);
                    if (motor.FeetHeightMeters > peak) peak = motor.FeetHeightMeters;
                }
                return peak;
            }

            float at120 = PeakWithStep(1f / 120f);
            float at30 = PeakWithStep(1f / 30f);

            Assert.That(at30, Is.EqualTo(at120).Within(0.15f),
                "Apex height drifted more than 15cm between 30 and 120 FPS.");
        }

        [Test]
        public void ResetReturnsToInitialState()
        {
            var motor = NewMotor();
            motor.Tick(Step, PlayerIntent.Jump);
            Advance(motor, 0.2f);

            motor.Reset();

            Assert.That(motor.Stance, Is.EqualTo(PlayerStance.Running));
            Assert.That(motor.FeetHeightMeters, Is.Zero);
        }
    }
}
