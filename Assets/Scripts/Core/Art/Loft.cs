using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // One cross-section along a lofted limb or body.
    public struct LoftStation
    {
        public Vec3 Center;

        // Half-extents of the cross-section. Up and down are separate because no part of an
        // animal is symmetric about its own spine — a theropod's ribcage is shallow above the
        // vertebrae and deep below them, and collapsing that into one radius is most of what
        // makes a generated creature read as a tube with legs.
        public float HalfWidth;
        public float HalfHeightUp;
        public float HalfHeightDown;

        // Superellipse exponent. 2 is a true ellipse; above that the section squares off
        // (jaws, foot pads, tail base); below it pinches into a lens (crests, claws).
        public float Squareness;

        public float V;
        public int BoneA;
        public int BoneB;
        public float WeightA;

        public LoftStation(Vec3 center, float halfWidth, float halfHeightUp, float halfHeightDown,
            float squareness, float v, int boneA, int boneB, float weightA)
        {
            Center = center;
            HalfWidth = halfWidth;
            HalfHeightUp = halfHeightUp;
            HalfHeightDown = halfHeightDown;
            Squareness = squareness;
            V = v;
            BoneA = boneA;
            BoneB = boneB;
            WeightA = weightA;
        }

        public static LoftStation Round(Vec3 center, float halfWidth, float halfHeight, float v, int bone) =>
            new LoftStation(center, halfWidth, halfHeight, halfHeight, 2f, v, bone, bone, 1f);
    }

    // Sweeps a cross-section along a poly-line to make a closed tube. Every organic part of the
    // dinosaur — body, tail, neck, skull, every limb segment, every toe — is one of these, which
    // is the reason a whole animal fits in a few hundred lines and why a second species is a
    // table of numbers rather than new geometry code (CLAUDE.md section 50).
    public static class Loft
    {
        public static void Build(
            MeshBuffer mesh,
            IReadOnlyList<LoftStation> stations,
            int segments,
            Vec3 referenceUp,
            bool capStart = true,
            bool capEnd = true)
        {
            if (stations == null || stations.Count < 2) throw new ArgumentException("A loft needs at least two stations.", nameof(stations));
            if (segments < 3) throw new ArgumentOutOfRangeException(nameof(segments));

            // segments + 1 vertices per ring: the last duplicates the first in position but
            // carries u = 1, so the texture wraps without a visible seam of stretched pixels.
            int perRing = segments + 1;
            int firstVertex = mesh.VertexCount;

            // Rotation-minimising frames, carried station to station, rather than Gram-Schmidt
            // against a fixed up at each one. The difference is not cosmetic: the neck chain
            // turns through ninety degrees from horizontal to vertical, and a fixed up is
            // undefined exactly where the tangent reaches vertical — which is the middle of
            // the throat. Transporting the previous frame has no singularity and, as a bonus,
            // introduces no twist, so the texture's stripes stay parallel all the way up.
            var previousTangent = Tangent(stations, 0);
            var up = InitialUp(previousTangent, referenceUp);

            for (int i = 0; i < stations.Count; i++)
            {
                var station = stations[i];
                var tangent = Tangent(stations, i);

                up = Transport(up, previousTangent, tangent);
                previousTangent = tangent;

                var right = Vec3.Cross(tangent, up).Normalised;

                for (int s = 0; s <= segments; s++)
                {
                    double theta = 2.0 * Math.PI * s / segments;
                    Section(theta, station.Squareness, out float cx, out float sy);

                    float radiusY = sy >= 0f ? station.HalfHeightUp : station.HalfHeightDown;
                    var position = station.Center + right * (station.HalfWidth * cx) + up * (radiusY * sy);

                    mesh.AddVertex(position, new Vec2((float)s / segments, station.V),
                        station.BoneA, station.BoneB, station.WeightA);
                }
            }

            for (int i = 0; i < stations.Count - 1; i++)
            {
                int ringA = firstVertex + i * perRing;
                int ringB = ringA + perRing;

                for (int s = 0; s < segments; s++)
                    mesh.AddQuad(ringA + s, ringB + s, ringB + s + 1, ringA + s + 1);
            }

            if (capStart) Cap(mesh, stations[0], firstVertex, segments, facingForward: false);
            if (capEnd) Cap(mesh, stations[stations.Count - 1], firstVertex + (stations.Count - 1) * perRing, segments, facingForward: true);
        }

        // A triangle fan to a centre vertex. Used at the tail tip, snout and finger ends, where
        // the ring has already shrunk to almost nothing and the cap is a few pixels.
        private static void Cap(MeshBuffer mesh, LoftStation station, int ringStart, int segments, bool facingForward)
        {
            int centre = mesh.AddVertex(station.Center, new Vec2(0.5f, station.V),
                station.BoneA, station.BoneB, station.WeightA);

            for (int s = 0; s < segments; s++)
            {
                if (facingForward) mesh.AddTriangle(centre, ringStart + s, ringStart + s + 1);
                else mesh.AddTriangle(centre, ringStart + s + 1, ringStart + s);
            }
        }

        private static void Section(double theta, float squareness, out float cx, out float sy)
        {
            double c = Math.Cos(theta);
            double s = Math.Sin(theta);

            if (Math.Abs(squareness - 2f) < 1e-4f)
            {
                cx = (float)c;
                sy = (float)s;
                return;
            }

            double e = 2.0 / squareness;
            cx = (float)(Math.Sign(c) * Math.Pow(Math.Abs(c), e));
            sy = (float)(Math.Sign(s) * Math.Pow(Math.Abs(s), e));
        }

        private static Vec3 Tangent(IReadOnlyList<LoftStation> stations, int index)
        {
            // Central difference in the interior so the frame turns smoothly through a curve
            // (the neck's S-bend is the case that shows up if this is one-sided).
            if (index == 0) return (stations[1].Center - stations[0].Center).Normalised;
            if (index == stations.Count - 1) return (stations[index].Center - stations[index - 1].Center).Normalised;
            return (stations[index + 1].Center - stations[index - 1].Center).Normalised;
        }

        // Gram-Schmidt, used once to seed the chain. Callers pass a reference that is not
        // parallel to the chain's first tangent; if it is, any perpendicular will do, because
        // the choice only sets where u = 0 falls around the first ring.
        private static Vec3 InitialUp(Vec3 tangent, Vec3 referenceUp)
        {
            var t = tangent.Normalised;
            var projected = referenceUp - t * Vec3.Dot(t, referenceUp);

            if (projected.Magnitude < 1e-4f)
            {
                var fallback = Math.Abs(t.Y) > 0.9f ? new Vec3(1f, 0f, 0f) : new Vec3(0f, 1f, 0f);
                projected = fallback - t * Vec3.Dot(t, fallback);
            }

            return projected.Normalised;
        }

        // Rotates `up` by the same rotation that takes one tangent to the next, which is the
        // smallest rotation that keeps the frame perpendicular to the curve.
        private static Vec3 Transport(Vec3 up, Vec3 fromTangent, Vec3 toTangent)
        {
            var axis = Vec3.Cross(fromTangent, toTangent);
            float sin = axis.Magnitude;
            float cos = Vec3.Dot(fromTangent, toTangent);

            if (sin < 1e-5f)
            {
                // Either no turn at all, or a reversal. A reversal cannot happen in a chain of
                // stations that advances monotonically, so this is the no-turn case.
                return cos >= 0f ? up : -up;
            }

            var rotated = Quat.AxisAngle(axis, (float)Math.Atan2(sin, cos)) * up;

            // Re-orthogonalise: transported frames accumulate float error over a long chain,
            // and a ring built from a frame that has drifted off perpendicular is visibly
            // sheared.
            var t = toTangent.Normalised;
            return (rotated - t * Vec3.Dot(t, rotated)).Normalised;
        }
    }
}
