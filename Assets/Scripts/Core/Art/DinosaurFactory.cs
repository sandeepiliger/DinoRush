using System;

namespace DinoRush.Core
{
    // Builds a rig whose *animated* silhouette matches the collision box, not merely its rest
    // pose's.
    //
    // This exists because of a mismatch that is easy to miss and impossible to live with. The
    // mesh builder can size the model exactly, but it sizes the rig standing at attention: the
    // running pose leans forward, bobs, and lowers the head, and comes out several centimetres
    // shorter. The collision box does not lean. Left alone the player dies to overhead obstacles
    // that visibly cleared their head — which reads as the game cheating, and is precisely the
    // fairness section 48 is about, seen from the other side.
    //
    // So: build once, measure what the animator actually produces, and rebuild at the scale
    // that lands the running silhouette on the box. Two passes, done at load, and the model can
    // never drift out of agreement with the hitbox no matter how the proportions are tuned.
    public static class DinosaurFactory
    {
        // Frames sampled across one stride when measuring. The silhouette breathes with the
        // gait, and the number that matters is the tallest the animal ever gets — matching the
        // average would leave it poking out of the top of its own hitbox twice per stride.
        private const int SilhouetteSamples = 24;

        private const float SettleSeconds = 3f;
        private const float SettleStep = 1f / 60f;

        public static DinosaurRig Create(DinosaurProfile profile, PlayerMotorConfig motor,
            DinosaurDetail detail = DinosaurDetail.High)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (motor == null) throw new ArgumentNullException(nameof(motor));

            float target = motor.StandingHeightMeters;

            var trial = DinosaurMeshBuilder.Build(profile, target, detail);
            float running = MeasureSilhouette(trial, PlayerStance.Running, ReferenceSpeed);

            if (running <= 1e-3f) return trial;

            // One Newton step is exact here: silhouette height is linear in the build scale.
            return DinosaurMeshBuilder.Build(profile, target * target / running, detail);
        }

        // The tallest the skinned mesh gets over a full stride in the given stance.
        public static float MeasureSilhouette(DinosaurRig rig, PlayerStance stance, float speed)
        {
            if (rig == null) throw new ArgumentNullException(nameof(rig));

            var animator = new DinosaurAnimator(rig.Skeleton, rig.Bones);
            var input = new DinosaurAnimationInput
            {
                Stance = stance,
                SpeedMetersPerSecond = speed,
            };

            // Settle first: the animator's action weights ease in, so a measurement taken
            // immediately after a reset is of a pose halfway into a crouch.
            for (float t = 0f; t < SettleSeconds; t += SettleStep) animator.Tick(SettleStep, input);

            var posed = new PosedSkeleton(rig.Skeleton.Count);
            float tallest = 0f;

            for (int sample = 0; sample < SilhouetteSamples; sample++)
            {
                posed.Resolve(rig.Skeleton, animator.Pose);
                tallest = Math.Max(tallest, Height(rig, posed));

                // Advance exactly one stride across all the samples.
                animator.Tick(StrideSeconds(speed) / SilhouetteSamples, input);
            }

            return tallest;
        }

        private static float Height(DinosaurRig rig, PosedSkeleton posed)
        {
            var mesh = rig.Mesh;
            float min = float.MaxValue, max = float.MinValue;

            for (int i = 0; i < mesh.VertexCount; i++)
            {
                float y = posed.Skin(rig.Skeleton, mesh.Positions[i], mesh.BoneA[i], mesh.BoneB[i], mesh.WeightA[i]).Y;
                if (y < min) min = y;
                if (y > max) max = y;
            }

            return max - min;
        }

        // Mid-range speed: the silhouette is measured where the game spends most of its time,
        // not at either extreme of the difficulty curve. The gait's effect on height is small
        // and monotonic in speed, so the ends stay within a centimetre of this.
        public const float ReferenceSpeed = 11f;

        private static float StrideSeconds(float speed) => speed <= 0.01f ? 1f : 3.2f / speed;
    }
}
