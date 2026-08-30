using System;

namespace DinoRush.Core
{
    // A minimal 3D vector. Core cannot reference UnityEngine (docs/DECISIONS.md D9), and the
    // camera framing rules below have to be testable outside the editor — that is the whole
    // point of putting them here. The Runtime layer converts to UnityEngine.Vector3 at the
    // boundary, exactly as it does for PaletteColor.
    public readonly struct Vec3
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public Vec3(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
        }

        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator *(float s, Vec3 a) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);

        public static Vec3 Lerp(Vec3 a, Vec3 b, float t) => a + (b - a) * t;

        public float Magnitude => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        public Vec3 Normalised
        {
            get
            {
                float m = Magnitude;
                return m <= 1e-6f ? new Vec3(0f, 0f, 1f) : new Vec3(X / m, Y / m, Z / m);
            }
        }

        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }
}
