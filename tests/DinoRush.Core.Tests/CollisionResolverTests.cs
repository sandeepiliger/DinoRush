using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class CollisionResolverTests
    {
        private const float Step = 1f / 60f;
        private static readonly PlayerMotorConfig Config = PlayerMotorConfig.CreateDefault();

        private static PlayerMotor NewMotor() => new PlayerMotor(Config);

        private static ObstacleSpawn Obstacle(float distance, PlayerAction action) =>
            new ObstacleSpawn(distance, RunGenerationConfig.CreateDefault().ObstacleWidthMeters, action);

        [Test]
        public void RunningIntoAGroundObstacleIsAHit()
        {
            var motor = NewMotor();
            Assert.That(CollisionResolver.IsHit(Config, motor, 10f, Obstacle(10f, PlayerAction.Jump)), Is.True);
        }

        [Test]
        public void JumpingOverAGroundObstacleClearsIt()
        {
            var motor = NewMotor();
            motor.Tick(Step, PlayerIntent.Jump);
            // Climb to well above the obstacle before testing overlap.
            for (int i = 0; i < 8; i++) motor.Tick(Step, PlayerIntent.None);

            Assert.That(motor.FeetHeightMeters, Is.GreaterThan(Config.JumpObstacleHeightMeters));
            Assert.That(CollisionResolver.IsHit(Config, motor, 10f, Obstacle(10f, PlayerAction.Jump)), Is.False);
        }

        [Test]
        public void RunningIntoAnOverheadObstacleIsAHit()
        {
            var motor = NewMotor();
            Assert.That(CollisionResolver.IsHit(Config, motor, 10f, Obstacle(10f, PlayerAction.Duck)), Is.True);
        }

        [Test]
        public void DuckingUnderAnOverheadObstacleClearsIt()
        {
            var motor = NewMotor();
            motor.Tick(Step, PlayerIntent.Duck);

            Assert.That(CollisionResolver.IsHit(Config, motor, 10f, Obstacle(10f, PlayerAction.Duck)), Is.False);
        }

        [Test]
        public void JumpingDoesNotClearAnOverheadObstacle()
        {
            // The two mechanics must stay distinct: jumping into an overhead obstacle should
            // still hit it, otherwise jump becomes a universal answer and duck is redundant.
            var motor = NewMotor();
            motor.Tick(Step, PlayerIntent.Jump);
            for (int i = 0; i < 8; i++) motor.Tick(Step, PlayerIntent.None);

            Assert.That(CollisionResolver.IsHit(Config, motor, 10f, Obstacle(10f, PlayerAction.Duck)), Is.True);
        }

        [Test]
        public void NoCollisionWhenHorizontallyClear()
        {
            var motor = NewMotor();

            Assert.That(CollisionResolver.IsHit(Config, motor, 0f, Obstacle(10f, PlayerAction.Jump)), Is.False,
                "An obstacle far ahead should not register.");
            Assert.That(CollisionResolver.IsHit(Config, motor, 20f, Obstacle(10f, PlayerAction.Jump)), Is.False,
                "An obstacle already passed should not register.");
        }

        [Test]
        public void EdgeContactCountsAsAHit()
        {
            var motor = NewMotor();
            var obstacle = Obstacle(10f, PlayerAction.Jump);

            // Player's leading edge just past the obstacle's trailing edge.
            float justTouching = obstacle.DistanceMeters - Config.PlayerHalfWidthMeters + 0.01f;

            Assert.That(CollisionResolver.IsHit(Config, motor, justTouching, obstacle), Is.True);
        }

        [Test]
        public void APlayerLandingBackOntoTheGroundHitsAGroundObstacle()
        {
            // Guards the completed-jump case: once feet are back down, a ground obstacle at
            // the same position must register again.
            var motor = NewMotor();
            motor.Tick(Step, PlayerIntent.Jump);
            for (float t = 0; t < Config.JumpAirtimeSeconds + 0.1f; t += Step)
                motor.Tick(Step, PlayerIntent.None);

            Assert.That(motor.Stance, Is.EqualTo(PlayerStance.Running));
            Assert.That(CollisionResolver.IsHit(Config, motor, 10f, Obstacle(10f, PlayerAction.Jump)), Is.True);
        }
    }
}
