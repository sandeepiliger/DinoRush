using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    // The camera shipped broken twice — once framed so tightly that the next obstacle was
    // always off-screen, once with the player himself outside the frustum. Both were invisible
    // to every test in the suite because framing lived in Unity as tuned constants.
    //
    // These are the tests that would have caught both.
    [TestFixture]
    public class RunCameraRigTests
    {
        private const float Aspect = RunCameraRig.PortraitAspect;
        private static readonly RunCameraRig Rig = RunCameraRig.CreateDefault();
        private static readonly PlayerMotorConfig Motor = PlayerMotorConfig.CreateDefault();

        // Somewhere down the track, so nothing depends on starting at the origin.
        private const float PlayerAt = 137.5f;

        private static Vec3 PlayerPoint(float feetHeight) =>
            new Vec3(PlayerAt, feetHeight + Motor.StandingHeightMeters * 0.5f, 0f);

        [Test]
        public void PlayerIsOnScreenWhileRunning()
        {
            Assert.That(Rig.IsInFrame(PlayerAt, PlayerPoint(0f), Aspect), Is.True,
                "The player was outside the frustum — exactly the bug where the capsule vanished.");
        }

        [Test]
        public void PlayerStaysOnScreenThroughAnEntireJump()
        {
            // Sampled across the arc, not just at the apex: the camera aims down the track, so
            // rising changes both the vertical and horizontal angle.
            for (int step = 0; step <= 20; step++)
            {
                float height = Motor.JumpApexMeters * (step / 20f);
                Rig.GetViewAngles(PlayerAt, PlayerPoint(height), out float h, out float v);

                Assert.That(h, Is.LessThanOrEqualTo(Rig.HorizontalHalfFovDegrees(Aspect)),
                    $"Player left the frame horizontally at jump height {height:F2}m.");
                Assert.That(v, Is.LessThanOrEqualTo(Rig.VerticalHalfFovDegrees),
                    $"Player left the frame vertically at jump height {height:F2}m.");
            }
        }

        [Test]
        public void PlayerHasMarginRatherThanSittingOnTheFrameEdge()
        {
            // Being just inside is not good enough: a device slightly narrower than the design's
            // aspect would push a marginal framing off-screen again.
            Rig.GetViewAngles(PlayerAt, PlayerPoint(0f), out float h, out float v);

            Assert.That(h, Is.LessThan(Rig.HorizontalHalfFovDegrees(Aspect) * 0.9f),
                "Player is within 10% of the horizontal frame edge — too tight to be safe.");
            Assert.That(v, Is.LessThan(Rig.VerticalHalfFovDegrees * 0.9f));
        }

        [Test]
        public void EnoughTrackIsVisibleToReactToTheNextObstacle()
        {
            // The first camera failure in miniature: with a 7.4m minimum gap, seeing less than
            // one gap ahead means the next obstacle appears already on top of the player.
            var run = RunGenerationConfig.CreateDefault();
            float required = run.MinObstacleGapMeters * 3f;

            var point = new Vec3(PlayerAt + required, 0.4f, 0f);

            Assert.That(Rig.IsInFrame(PlayerAt, point, Aspect), Is.True,
                $"Cannot see {required:F1}m down the track — under three obstacle gaps of warning.");
        }

        [Test]
        public void FortyMetresOfTrackIsVisible()
        {
            Assert.That(Rig.IsInFrame(PlayerAt, new Vec3(PlayerAt + 40f, 0f, 0f), Aspect), Is.True);
        }

        [Test]
        public void GroundBehindThePlayerIsNotClaimedVisible()
        {
            // Sanity on the maths itself: a point well behind the camera must never report as
            // in frame, or every other assertion here is meaningless.
            Assert.That(Rig.IsInFrame(PlayerAt, new Vec3(PlayerAt - 60f, 0f, 0f), Aspect), Is.False);
        }

        [Test]
        public void PortraitHorizontalViewIsNarrowerThanVertical()
        {
            // Documents the trap that caused both failures: fieldOfView is vertical, and in
            // portrait the horizontal half-angle is roughly half of it.
            Assert.That(Rig.HorizontalHalfFovDegrees(Aspect), Is.LessThan(Rig.VerticalHalfFovDegrees));
        }

        [Test]
        public void FramingSurvivesNarrowerAndWiderDevices()
        {
            // 9:21 ultra-tall through 3:4 tablet-ish. The player must stay visible on all of them.
            foreach (float aspect in new[] { 9f / 21f, 9f / 20f, 9f / 16f, 3f / 4f })
            {
                Assert.That(Rig.IsInFrame(PlayerAt, PlayerPoint(0f), aspect), Is.True,
                    $"Player is off-screen at aspect {aspect:F3}.");
                Assert.That(Rig.IsInFrame(PlayerAt, PlayerPoint(Motor.JumpApexMeters), aspect), Is.True,
                    $"Player is off-screen at jump apex at aspect {aspect:F3}.");
            }
        }

        [Test]
        public void CameraSitsBehindAndBesideThePlayer()
        {
            var position = Rig.GetPosition(PlayerAt);

            Assert.That(position.X, Is.LessThan(PlayerAt), "The camera must trail the player, not lead them.");
            Assert.That(position.Y, Is.GreaterThan(0f));
            Assert.That(Rig.SideMeters, Is.GreaterThan(0f),
                "Some side offset keeps the dinosaur readable in profile rather than as a rear silhouette.");
        }

        [Test]
        public void CameraAimsAheadOfThePlayerNotAtThem()
        {
            Assert.That(Rig.GetLookTarget(PlayerAt).X, Is.GreaterThan(PlayerAt),
                "Aiming at the player wastes the screen on track already run.");
        }
    }
}
