using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // The handful of shapes a swept cross-section cannot express: a ball (eyes), a flat blade
    // (feather fans, dorsal crest) and a curved spike (claws, horns).
    public static class Primitives
    {
        // A UV sphere. Rings and segments stay low deliberately — an eye is eight pixels across
        // in gameplay, and the only place it is ever seen large is the collection screen's
        // portrait, where the silhouette carries the read rather than the topology.
        public static void Sphere(MeshBuffer mesh, Vec3 centre, float radius, int rings, int segments,
            int bone, Vec2 uv, float flattenZ = 1f)
        {
            int first = mesh.VertexCount;

            for (int r = 0; r <= rings; r++)
            {
                double phi = Math.PI * r / rings;
                float y = (float)Math.Cos(phi);
                float ringRadius = (float)Math.Sin(phi);

                for (int s = 0; s <= segments; s++)
                {
                    double theta = 2.0 * Math.PI * s / segments;
                    var offset = new Vec3(
                        (float)Math.Cos(theta) * ringRadius * radius,
                        y * radius,
                        (float)Math.Sin(theta) * ringRadius * radius * flattenZ);

                    mesh.AddVertex(centre + offset, uv, bone, bone, 1f);
                }
            }

            int perRing = segments + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = first + r * perRing + s;
                    int b = a + perRing;
                    mesh.AddQuad(a, a + 1, b + 1, b);
                }
            }
        }

        // A flat, slightly thickened strip running along a spine of points, each with its own
        // outward height. Solid geometry rather than an alpha-cut plane on purpose: section 12
        // asks for minimal transparency, and overdraw from feather cards is exactly the kind of
        // mobile cost that is invisible in the editor and obvious on a phone.
        public static void Blade(MeshBuffer mesh, IReadOnlyList<BladeStation> stations, float thickness)
        {
            if (stations == null || stations.Count < 2) throw new ArgumentException("A blade needs at least two stations.", nameof(stations));

            int first = mesh.VertexCount;

            for (int i = 0; i < stations.Count; i++)
            {
                var station = stations[i];
                // Per-station thickness, defaulting to the blade's own. A constant-thickness
                // strip reads as a piece of card stuck to the model; a ridge that thins towards
                // its ends reads as part of the animal.
                float half = (station.Thickness > 0f ? station.Thickness : thickness) * 0.5f;
                var side = new Vec3(0f, 0f, half);

                mesh.AddVertex(station.Root - side, new Vec2(station.U, 0f), station.Bone, station.Bone, 1f);
                mesh.AddVertex(station.Tip - side, new Vec2(station.U, 1f), station.Bone, station.Bone, 1f);
                mesh.AddVertex(station.Tip + side, new Vec2(station.U, 1f), station.Bone, station.Bone, 1f);
                mesh.AddVertex(station.Root + side, new Vec2(station.U, 0f), station.Bone, station.Bone, 1f);
            }

            for (int i = 0; i < stations.Count - 1; i++)
            {
                int a = first + i * 4;
                int b = a + 4;

                mesh.AddQuad(a + 0, a + 1, b + 1, b + 0); // near face
                mesh.AddQuad(b + 3, b + 2, a + 2, a + 3); // far face
                mesh.AddQuad(a + 1, a + 2, b + 2, b + 1); // outer edge
                mesh.AddQuad(b + 0, b + 3, a + 3, a + 0); // inner edge
            }
        }

        // A tapered, curved spike swept from base to tip. Claws curve; a straight cone reads as
        // a traffic cone stuck to a foot.
        public static void Claw(MeshBuffer mesh, Vec3 baseCentre, Vec3 direction, Vec3 curveAxis,
            float length, float baseRadius, float curveRadians, int bone, int segments = 6, int steps = 4)
        {
            var stations = new List<LoftStation>(steps + 1);
            var forward = direction.Normalised;

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                // Progressive bend: the tip curves several times as far as the mid-point, which
                // is what gives a claw its hook rather than a uniform arc.
                var bent = Quat.AxisAngle(curveAxis, curveRadians * t * t) * forward;
                var centre = baseCentre + bent * (length * t);
                float radius = baseRadius * (1f - t) * (1f - t * 0.35f);

                stations.Add(LoftStation.Round(centre, radius, radius, t, bone));
            }

            Loft.Build(mesh, stations, segments, new Vec3(0f, 1f, 0f), capStart: true, capEnd: true);
        }
    }

    public struct BladeStation
    {
        public Vec3 Root;
        public Vec3 Tip;
        public float U;
        public int Bone;

        // Zero means "use the blade's default".
        public float Thickness;

        public BladeStation(Vec3 root, Vec3 tip, float u, int bone, float thickness = 0f)
        {
            Root = root;
            Tip = tip;
            U = u;
            Bone = bone;
            Thickness = thickness;
        }
    }
}
