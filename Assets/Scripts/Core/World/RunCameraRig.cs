using System;

namespace DinoRush.Core
{
    // Where the camera sits, where it aims, and — the part that matters — whether a given point
    // is actually on screen.
    //
    // This lives in Core because the camera has now been wrong twice, both times because the
    // numbers were tuned blind and could only be checked by pressing Play. Framing is pure
    // geometry, so it can be a tested invariant instead: RunCameraRigTests asserts the player
    // stays inside the frustum at every point of a jump, and that enough track ahead is visible
    // to react to. A bad camera now fails CI rather than shipping.
    //
    // The trap both failures shared: Camera.fieldOfView is VERTICAL. In portrait (aspect ~0.46)
    // the horizontal half-angle is barely half the vertical one, so a side offset that looks
    // harmless swings the player out of frame entirely.
    public sealed class RunCameraRig
    {
        public float TrailMeters { get; }
        public float HeightMeters { get; }
        public float SideMeters { get; }
        public float LookAheadMeters { get; }
        public float VerticalFovDegrees { get; }
        public float LookAtHeightMeters { get; }

        public RunCameraRig(
            float trailMeters, float heightMeters, float sideMeters,
            float lookAheadMeters, float verticalFovDegrees, float lookAtHeightMeters = 1.2f)
        {
            if (trailMeters <= 0) throw new ArgumentOutOfRangeException(nameof(trailMeters));
            if (verticalFovDegrees <= 0 || verticalFovDegrees >= 180) throw new ArgumentOutOfRangeException(nameof(verticalFovDegrees));

            TrailMeters = trailMeters;
            HeightMeters = heightMeters;
            SideMeters = sideMeters;
            LookAheadMeters = lookAheadMeters;
            VerticalFovDegrees = verticalFovDegrees;
            LookAtHeightMeters = lookAtHeightMeters;
        }

        // Behind the player, raised, and slightly to one side. The side offset is what keeps the
        // dinosaur readable in three-quarter profile rather than as a rear silhouette — but it
        // is the value most likely to push the player out of a portrait frame, which is why the
        // tests pin it.
        public Vec3 GetPosition(float playerDistanceMeters) =>
            new Vec3(playerDistanceMeters - TrailMeters, HeightMeters, -SideMeters);

        // Aims down the track rather than at the player, so most of the screen is what's coming.
        public Vec3 GetLookTarget(float playerDistanceMeters) =>
            new Vec3(playerDistanceMeters + LookAheadMeters, LookAtHeightMeters, 0f);

        public Vec3 GetForward(float playerDistanceMeters) =>
            (GetLookTarget(playerDistanceMeters) - GetPosition(playerDistanceMeters)).Normalised;

        // Horizontal half-angle derived from the vertical FOV and the aspect ratio. This is the
        // number that makes portrait framing so unforgiving.
        public float HorizontalHalfFovDegrees(float aspect)
        {
            if (aspect <= 0) throw new ArgumentOutOfRangeException(nameof(aspect));
            double vertical = Math.Tan(VerticalFovDegrees * Math.PI / 360.0);
            return (float)(Math.Atan(vertical * aspect) * 180.0 / Math.PI);
        }

        public float VerticalHalfFovDegrees => VerticalFovDegrees * 0.5f;

        // Angles of a world point away from the camera's forward axis, in degrees.
        public void GetViewAngles(float playerDistanceMeters, Vec3 point, out float horizontal, out float vertical)
        {
            var position = GetPosition(playerDistanceMeters);
            var forward = GetForward(playerDistanceMeters);

            // Matches how Quaternion.LookRotation builds a basis from a world up of (0,1,0).
            var right = Vec3.Cross(new Vec3(0f, 1f, 0f), forward).Normalised;
            var up = Vec3.Cross(forward, right).Normalised;

            var toPoint = point - position;
            float depth = Vec3.Dot(toPoint, forward);

            if (depth <= 0f)
            {
                // Behind the camera — report a straight angle so it can never read as visible.
                horizontal = vertical = 180f;
                return;
            }

            horizontal = (float)(Math.Atan2(Math.Abs(Vec3.Dot(toPoint, right)), depth) * 180.0 / Math.PI);
            vertical = (float)(Math.Atan2(Math.Abs(Vec3.Dot(toPoint, up)), depth) * 180.0 / Math.PI);
        }

        public bool IsInFrame(float playerDistanceMeters, Vec3 point, float aspect)
        {
            GetViewAngles(playerDistanceMeters, point, out float horizontal, out float vertical);
            return horizontal <= HorizontalHalfFovDegrees(aspect) && vertical <= VerticalHalfFovDegrees;
        }

        // The design's 390x844 portrait layout — the aspect every framing rule is checked against.
        public const float PortraitAspect = 390f / 844f;

        // Solved against the framing tests rather than eyeballed: keeps the player comfortably
        // inside the portrait frame through a full jump while still showing ~45m of track.
        public static RunCameraRig CreateDefault() => new RunCameraRig(
            trailMeters: 10f,
            heightMeters: 3.5f,
            sideMeters: 3.5f,
            lookAheadMeters: 22f,
            verticalFovDegrees: 68f);
    }
}
