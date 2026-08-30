using System;

namespace DinoRush.Core
{
    // A unit quaternion, with the same component order and multiplication convention as
    // UnityEngine.Quaternion so the Runtime layer can hand components straight across.
    //
    // Core needs its own because the dinosaur's pose is evaluated here, not in Unity: that is
    // what makes "the ducking silhouette is really 0.9m tall" and "the planted foot really
    // does not slide" assertable in `dotnet test` rather than judged by eye (D9).
    public readonly struct Quat
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float W { get; }

        public Quat(float x, float y, float z, float w)
        {
            X = x; Y = y; Z = z; W = w;
        }

        public static Quat Identity => new Quat(0f, 0f, 0f, 1f);

        public static Quat AxisAngle(Vec3 axis, float radians)
        {
            var a = axis.Normalised;
            float half = radians * 0.5f;
            float s = (float)Math.Sin(half);
            return new Quat(a.X * s, a.Y * s, a.Z * s, (float)Math.Cos(half));
        }

        // Rotation in the side-on plane: positive pitches the bone's local +X downward, which
        // is the direction every limb and spine joint in the rig bends.
        public static Quat Pitch(float radians) => AxisAngle(new Vec3(0f, 0f, 1f), radians);

        // Rotation about the vertical, used for the head turn and the tail's counter-sway.
        public static Quat Yaw(float radians) => AxisAngle(new Vec3(0f, 1f, 0f), radians);

        // Rotation about the forward axis, used for the body roll of a running gait.
        public static Quat Roll(float radians) => AxisAngle(new Vec3(1f, 0f, 0f), radians);

        public static Quat operator *(Quat a, Quat b) => new Quat(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

        public static Vec3 operator *(Quat q, Vec3 v)
        {
            // v + 2w(q x v) + 2(q x (q x v)) — the standard expansion, which avoids building
            // a matrix for what is always a single vector.
            var u = new Vec3(q.X, q.Y, q.Z);
            var uv = Vec3.Cross(u, v);
            var uuv = Vec3.Cross(u, uv);
            return v + (uv * q.W + uuv) * 2f;
        }

        public Quat Conjugate => new Quat(-X, -Y, -Z, W);

        public Quat Normalised
        {
            get
            {
                float m = (float)Math.Sqrt(X * X + Y * Y + Z * Z + W * W);
                return m <= 1e-6f ? Identity : new Quat(X / m, Y / m, Z / m, W / m);
            }
        }

        // Shortest-path nlerp. Normalised linear interpolation rather than slerp because every
        // blend in the animator is between nearby poses, where the two are visually identical
        // and nlerp costs a fraction as much per bone per frame (section 35).
        public static Quat Nlerp(Quat a, Quat b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;

            float dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
            float sign = dot < 0f ? -1f : 1f;

            return new Quat(
                a.X + (b.X * sign - a.X) * t,
                a.Y + (b.Y * sign - a.Y) * t,
                a.Z + (b.Z * sign - a.Z) * t,
                a.W + (b.W * sign - a.W) * t).Normalised;
        }

        public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3}, {W:F3})";
    }
}
