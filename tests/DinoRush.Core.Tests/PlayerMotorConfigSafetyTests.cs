using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    // These are the tests that make the tuning numbers in PlayerMotorConfig accountable to
    // CLAUDE.md section 48 ("no impossible jump", "no unavoidable obstacle"). Section 48's
    // spacing floor is only meaningful if the player's actual movement can exploit it — a
    // generator that spaces obstacles perfectly still produces an unwinnable game if the jump
    // can't clear them. Retuning jump or gravity without re-checking these fails CI.
    [TestFixture]
    public class PlayerMotorConfigSafetyTests
    {
        private static readonly PlayerMotorConfig Motor = PlayerMotorConfig.CreateDefault();
        private static readonly RunGenerationConfig Run = RunGenerationConfig.CreateDefault();

        [Test]
        public void JumpClearsGroundObstacles()
        {
            Assert.That(Motor.JumpApexMeters, Is.GreaterThan(Motor.JumpObstacleHeightMeters),
                "The jump apex must exceed the height of a ground obstacle, or Jump obstacles are impossible.");
        }

        [Test]
        public void JumpClearsGroundObstaclesWithUsableMargin()
        {
            // Clearing by a hair is technically passable but feels broken and leaves no room
            // for input latency. Require at least 25% headroom over the obstacle.
            Assert.That(Motor.JumpApexMeters, Is.GreaterThan(Motor.JumpObstacleHeightMeters * 1.25f));
        }

        [Test]
        public void PlayerLandsBeforeTheNextObstacleCanArrive()
        {
            // The critical one. If a jump's airtime outlasts the minimum spacing at top speed,
            // a player who jumps one obstacle can still be airborne when the next arrives —
            // and if that next obstacle requires ducking, it is unavoidable by construction,
            // which is exactly what section 48 forbids.
            float airborneDistance = Motor.JumpAirtimeSeconds * Run.MaxRunSpeedMetersPerSecond;

            Assert.That(airborneDistance, Is.LessThan(Run.MinObstacleGapMeters),
                $"A jump covers {airborneDistance:F2}m at top speed but obstacles may be only " +
                $"{Run.MinObstacleGapMeters:F2}m apart — the player could be stranded airborne into a duck obstacle.");
        }

        [Test]
        public void DuckingClearsOverheadObstacles()
        {
            Assert.That(Motor.DuckingHeightMeters, Is.LessThan(Motor.DuckObstacleBottomMeters),
                "A ducking player must fit under an overhead obstacle, or Duck obstacles are impossible.");
        }

        [Test]
        public void StandingDoesNotClearOverheadObstacles()
        {
            Assert.That(Motor.StandingHeightMeters, Is.GreaterThan(Motor.DuckObstacleBottomMeters),
                "If a standing player already fits under an overhead obstacle, ducking is pointless.");
        }

        [Test]
        public void DuckLastsLongEnoughToTraverseAnObstacle()
        {
            float timeToCross = (Run.ObstacleWidthMeters + Motor.PlayerHalfWidthMeters * 2f) / Run.MaxRunSpeedMetersPerSecond;

            Assert.That(Motor.DuckDurationSeconds, Is.GreaterThan(timeToCross),
                "The duck must outlast the time needed to pass through an obstacle at top speed, " +
                "or the player stands back up mid-obstacle.");
        }

        [Test]
        public void ReactionTimeAllowsAJumpToCompleteInTheGap()
        {
            // The spacing floor is reaction time plus margin; a jump must fit inside what's
            // left after the player has reacted.
            float gapTravelTime = Run.MinObstacleGapMeters / Run.MaxRunSpeedMetersPerSecond;

            Assert.That(Motor.JumpAirtimeSeconds, Is.LessThan(gapTravelTime),
                "Jump airtime must fit within the time it takes to cross the minimum obstacle gap.");
        }
    }
}
