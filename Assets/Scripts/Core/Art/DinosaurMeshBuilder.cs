using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    public enum DinosaurDetail
    {
        // LOD0/1/2 of section 12. Detail is a build-time parameter rather than a decimation
        // pass because the model is generated: asking for fewer ring segments costs nothing and
        // produces a cleaner low-poly mesh than throwing triangles away from a dense one.
        High,
        Medium,
        Low,
    }

    // Named bone indices, so the animator can say "the left knee" instead of "bone 14".
    public sealed class DinosaurBones
    {
        public int Root { get; internal set; }
        public int Hips { get; internal set; }
        public int Spine { get; internal set; }
        public int Chest { get; internal set; }
        public int NeckLow { get; internal set; }
        public int NeckHigh { get; internal set; }
        public int Head { get; internal set; }
        public int Jaw { get; internal set; }

        // Base to tip.
        public int[] Tail { get; internal set; }

        // Thigh, shin, metatarsus, toe.
        public int[] LegLeft { get; internal set; }
        public int[] LegRight { get; internal set; }

        // Upper arm, forearm, hand.
        public int[] ArmLeft { get; internal set; }
        public int[] ArmRight { get; internal set; }

        // bone index -> its mirror across the body's centre plane.
        public int[] Mirror { get; internal set; }
    }

    // A finished, rigged dinosaur: geometry, skeleton and the measurements the rest of the game
    // needs to trust it.
    public sealed class DinosaurRig
    {
        public DinosaurProfile Profile { get; internal set; }
        public MeshBuffer Mesh { get; internal set; }
        public Skeleton Skeleton { get; internal set; }
        public DinosaurBones Bones { get; internal set; }

        // Silhouette height in metres with the rig at rest. Built to equal
        // PlayerMotorConfig.StandingHeightMeters, and asserted to.
        public float StandingHeightMeters { get; internal set; }

        // How far the snout reaches past the model's origin, and how far the tail trails it.
        // The first of these is a fairness budget: it is how far the nose enters an obstacle
        // before the collision box does.
        public float ForwardExtentMeters { get; internal set; }
        public float RearExtentMeters { get; internal set; }
    }

    // Generates the dinosaur. See DinosaurProfile for why the shape is data and this is not.
    public static class DinosaurMeshBuilder
    {
        public static DinosaurRig Build(DinosaurProfile profile, float standingHeightMeters,
            DinosaurDetail detail = DinosaurDetail.High)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (standingHeightMeters <= 0f) throw new ArgumentOutOfRangeException(nameof(standingHeightMeters));

            int segments = detail == DinosaurDetail.High ? 14 : detail == DinosaurDetail.Medium ? 10 : 7;
            bool plumage = detail != DinosaurDetail.Low;

            var joints = new Joints(profile);
            var bones = new DinosaurBones();
            var skeleton = BuildSkeleton(profile, joints, bones);

            var mesh = new MeshBuffer();
            BuildBody(mesh, profile, joints, bones, segments);
            BuildJaw(mesh, profile, joints, bones, segments);
            if (plumage) BuildCrest(mesh, profile, joints, bones);
            if (plumage && profile.TailFeatherLength > 0f) BuildTailFan(mesh, profile, joints, bones);

            // Everything from here is one side of a symmetric pair, mirrored in one go below.
            int mirrorFromVertex = mesh.VertexCount;
            int mirrorFromTriangle = mesh.Triangles.Count;

            BuildEye(mesh, profile, joints, bones, detail);
            BuildLeg(mesh, profile, joints, bones.LegLeft, segments, detail);
            BuildArm(mesh, profile, joints, bones.ArmLeft, segments, plumage);

            mesh.AppendMirroredZ(mirrorFromVertex, mirrorFromTriangle, bones.Mirror);

            // Canonical units are "one unit tall"; only now, with every part present, is the
            // real silhouette height known. Rescaling here rather than baking a factor into
            // the profile is what keeps the model exactly as tall as the collision box after
            // any proportion changes.
            mesh.GetBounds(out var min, out var max);
            float scale = standingHeightMeters / (max.Y - min.Y);

            // Origin at mid-torso, so the feet sit centred on the collision box. The
            // alternative — origin at the hips — leaves the whole animal ahead of its own
            // hitbox; origin at the snout leaves its legs behind it. Neither reads as the
            // dinosaur being where the game says it is.
            float pivotX = joints.SpineMid.X;
            var offset = new Vec3(-pivotX * scale, -min.Y * scale, 0f);

            var scaledMesh = Transform(mesh, scale, offset);
            var scaledSkeleton = Transform(skeleton, scale, offset);
            scaledMesh.RecalculateNormals();

            scaledMesh.GetBounds(out var finalMin, out var finalMax);

            return new DinosaurRig
            {
                Profile = profile,
                Mesh = scaledMesh,
                Skeleton = scaledSkeleton,
                Bones = bones,
                StandingHeightMeters = finalMax.Y - finalMin.Y,
                ForwardExtentMeters = finalMax.X,
                RearExtentMeters = finalMin.X,
            };
        }

        // ---------------------------------------------------------------------------------
        // Joint layout
        // ---------------------------------------------------------------------------------

        // Every joint position, in canonical units, derived once so the skeleton and the
        // geometry cannot disagree about where the animal's knee is.
        private sealed class Joints
        {
            public readonly Vec3 Pelvis, Waist, Ribcage, SpineMid, Chest, Shoulder;
            public readonly Vec3 NeckLow, NeckMid, NeckTop;
            public readonly Vec3 HeadBase, Occiput, Orbit, SnoutTip;
            public readonly Vec3 JawHinge;

            // Occiput to snout tip. The mandible is derived from these rather than authored
            // beside them — see BuildJaw for why that is the difference between a closed mouth
            // and a pelican.
            public readonly SkullSection[] Skull;
            public readonly Vec3 Hip, Knee, Ankle, Ball, ToeTip;
            public readonly Vec3 ArmRoot, Elbow, Wrist, FingerTip;
            public readonly Vec3[] TailPath;

            public Joints(DinosaurProfile p)
            {
                // The hip *joint* sits below the spine, not on it. Collapsing the two — which
                // the first pass did — is what leaves the thigh emerging from the middle of the
                // flank instead of from underneath the pelvis.
                float hipY = p.HipHeight;
                float spineY = hipY + p.BellyDepth * 0.23f;

                // Five trunk stations, not three. A theropod's trunk is not a taper: it is a
                // wide pelvis, a pinched waist, a deep ribcage, and a narrowing chest, and it is
                // that sequence — far more than polygon count — that separates an animal from a
                // potato with legs.
                Pelvis = new Vec3(0f, spineY, 0f);
                Waist = new Vec3(p.TorsoLength * 0.30f, spineY + 0.012f, 0f);
                Ribcage = new Vec3(p.TorsoLength * 0.62f, spineY + 0.020f, 0f);
                SpineMid = Ribcage;
                Chest = new Vec3(p.TorsoLength * 0.88f, spineY + 0.028f, 0f);
                Shoulder = new Vec3(p.TorsoLength, spineY + 0.040f, 0f);

                // The neck's S: rising and drawn slightly back off the shoulders, then forward
                // again to carry the head over the chest. A straight neck is the single most
                // reliable way to make a theropod look like a toy — and a neck that only rises,
                // with the head bolted on at right angles, produces a llama.
                NeckLow = Shoulder + new Vec3(p.NeckCurve * 0.30f, p.NeckLength * 0.30f, 0f);
                NeckMid = Shoulder + new Vec3(p.NeckCurve * 0.10f, p.NeckLength * 0.66f, 0f);
                NeckTop = Shoulder + new Vec3(p.NeckCurve * 0.95f, p.NeckLength, 0f);

                // The skull carries on the neck's forward lean rather than turning out of it,
                // and drops towards the snout so the head is held nose-down at a hunt angle.
                HeadBase = NeckTop + new Vec3(p.SkullLength * 0.10f, p.SkullDepth * 0.34f, 0f);
                Occiput = HeadBase;

                // Five named landmarks down the skull, each with its own width and its own
                // split above and below the axis. The proportions are a theropod's: a deep
                // braincase, a brow that overhangs the eye, a waist at the antorbital gap, then
                // a muzzle. Read the Down column top to bottom and it traces the jawline, which
                // comes out almost level — that is the shape being aimed at.
                Skull = new[]
                {
                    new SkullSection(0.00f, 0.00f, 1.16f, 0.98f, 0.90f, 2.5f),
                    new SkullSection(0.28f, 0.14f, 1.10f, 0.92f, 0.78f, 2.6f),
                    new SkullSection(0.52f, 0.38f, 0.82f, 0.66f, 0.56f, 2.5f),
                    new SkullSection(0.78f, 0.70f, 0.62f, 0.48f, 0.40f, 2.4f),
                    new SkullSection(1.00f, 1.00f, 0.28f, 0.24f, 0.18f, 2.2f),
                };

                for (int i = 0; i < Skull.Length; i++)
                    Skull[i].Centre = HeadBase + new Vec3(
                        p.SkullLength * Skull[i].T, -p.SnoutDrop * Skull[i].Drop, 0f);

                Orbit = Skull[1].Centre;
                SnoutTip = Skull[4].Centre;

                // The jaw hinges at the back of the skull, under the ear, not halfway down the
                // muzzle. Hinging it forward — which the first pass did — leaves the mandible
                // as a bar floating in front of the face with a gap behind it, and no amount of
                // adjusting its thickness hides that it is not attached to anything.
                JawHinge = Occiput + new Vec3(p.SkullLength * 0.06f, -p.SkullDepth * 0.68f, 0f);

                // Digitigrade hind limb. Each segment's vertical drop is whatever is left of
                // its length once the horizontal offset is taken out, so a segment can never be
                // stretched beyond the bone length the profile declares.
                float femurDrop = Drop(p.FemurLength, p.KneeForward, nameof(p.FemurLength));
                float tibiaDrop = Drop(p.TibiaLength, p.AnkleBack, nameof(p.TibiaLength));
                float metatarsalForward = p.AnkleBack * 0.62f;
                float metatarsalDrop = Drop(p.MetatarsusLength, metatarsalForward, nameof(p.MetatarsusLength));

                Hip = new Vec3(0f, hipY, p.HipSpacing);
                Knee = new Vec3(p.KneeForward, hipY - femurDrop, p.HipSpacing * 1.05f);
                Ankle = new Vec3(Knee.X - p.AnkleBack, Knee.Y - tibiaDrop, p.HipSpacing * 1.10f);
                Ball = new Vec3(Ankle.X + metatarsalForward, Ankle.Y - metatarsalDrop, p.HipSpacing * 1.12f);
                ToeTip = new Vec3(Ball.X + p.ToeLength, Ball.Y * 0.30f, p.HipSpacing * 1.12f);

                // Folded forelimb: elbow tucked back and down against the ribs, hand carried
                // forward. Raptor arms are held like this at rest and it reads far better in
                // silhouette than arms hanging straight down.
                ArmRoot = Chest + new Vec3(0.006f, -0.052f, p.TorsoWidth * 0.78f);
                Elbow = ArmRoot + new Vec3(-p.ArmLength * 0.30f, -p.ArmLength * 0.94f, 0.010f);
                Wrist = Elbow + new Vec3(p.ForearmLength * 0.85f, -p.ForearmLength * 0.50f, 0.004f);
                FingerTip = Wrist + new Vec3(p.ForearmLength * 0.42f, -p.ForearmLength * 0.16f, 0f);

                // The tail rises quadratically rather than linearly: held level off the hips
                // and lifting towards the tip, which is both how a counterbalancing tail is
                // carried and what keeps it above the 0.8m ground obstacles it sweeps over.
                TailPath = new Vec3[TailStationCount];
                for (int i = 0; i < TailStationCount; i++)
                {
                    float t = (float)i / (TailStationCount - 1);
                    TailPath[i] = new Vec3(-p.TailLength * t, spineY + p.TailRise * t * t, 0f);
                }
            }

            private static float Drop(float length, float horizontal, string name)
            {
                float squared = length * length - horizontal * horizontal;
                if (squared <= 1e-6f)
                    throw new ArgumentOutOfRangeException(name, $"{name} ({length}) is too short to span its horizontal offset ({horizontal}).");
                return (float)Math.Sqrt(squared);
            }
        }

        private const int TailStationCount = 9;

        // One cross-section of the upper skull. Widths and depths are multiples of the
        // profile's SkullWidth/SkullDepth so a species with a heavier head changes two numbers
        // rather than a table.
        private struct SkullSection
        {
            public Vec3 Centre;
            public readonly float T;
            public readonly float Drop;
            public readonly float Width;
            public readonly float Up;
            public readonly float Down;
            public readonly float Squareness;

            public SkullSection(float t, float drop, float width, float up, float down, float squareness)
            {
                Centre = default;
                T = t; Drop = drop; Width = width; Up = up; Down = down; Squareness = squareness;
            }
        }

        // ---------------------------------------------------------------------------------
        // Skeleton
        // ---------------------------------------------------------------------------------

        private static Skeleton BuildSkeleton(DinosaurProfile p, Joints j, DinosaurBones bones)
        {
            var list = new List<BoneDefinition>();
            var mirror = new List<int>();

            int Add(string name, int parent, Vec3 position)
            {
                list.Add(new BoneDefinition(name, parent, position));
                mirror.Add(list.Count - 1); // centred by default; limb pairs fix this up below
                return list.Count - 1;
            }

            bones.Root = Add("Root", -1, new Vec3(0f, 0f, 0f));
            bones.Hips = Add("Hips", bones.Root, j.Pelvis);
            bones.Spine = Add("Spine", bones.Hips, j.SpineMid);
            bones.Chest = Add("Chest", bones.Spine, j.Chest);
            bones.NeckLow = Add("NeckLow", bones.Chest, j.NeckLow);
            bones.NeckHigh = Add("NeckHigh", bones.NeckLow, j.NeckTop);
            bones.Head = Add("Head", bones.NeckHigh, j.HeadBase);
            bones.Jaw = Add("Jaw", bones.Head, j.JawHinge);

            bones.Tail = new int[p.TailBones];
            int parentBone = bones.Hips;
            for (int i = 0; i < p.TailBones; i++)
            {
                float t = (float)(i + 1) / p.TailBones;
                var position = SampleTail(j, t * 0.94f);
                bones.Tail[i] = Add($"Tail{i + 1}", parentBone, position);
                parentBone = bones.Tail[i];
            }

            bones.LegLeft = new int[4];
            bones.LegLeft[0] = Add("Thigh.L", bones.Hips, j.Hip);
            bones.LegLeft[1] = Add("Shin.L", bones.LegLeft[0], j.Knee);
            bones.LegLeft[2] = Add("Foot.L", bones.LegLeft[1], j.Ankle);
            bones.LegLeft[3] = Add("Toe.L", bones.LegLeft[2], j.Ball);

            bones.ArmLeft = new int[3];
            bones.ArmLeft[0] = Add("Arm.L", bones.Chest, j.ArmRoot);
            bones.ArmLeft[1] = Add("Forearm.L", bones.ArmLeft[0], j.Elbow);
            bones.ArmLeft[2] = Add("Hand.L", bones.ArmLeft[1], j.Wrist);

            bones.LegRight = new int[4];
            bones.LegRight[0] = Add("Thigh.R", bones.Hips, MirrorZ(j.Hip));
            bones.LegRight[1] = Add("Shin.R", bones.LegRight[0], MirrorZ(j.Knee));
            bones.LegRight[2] = Add("Foot.R", bones.LegRight[1], MirrorZ(j.Ankle));
            bones.LegRight[3] = Add("Toe.R", bones.LegRight[2], MirrorZ(j.Ball));

            bones.ArmRight = new int[3];
            bones.ArmRight[0] = Add("Arm.R", bones.Chest, MirrorZ(j.ArmRoot));
            bones.ArmRight[1] = Add("Forearm.R", bones.ArmRight[0], MirrorZ(j.Elbow));
            bones.ArmRight[2] = Add("Hand.R", bones.ArmRight[1], MirrorZ(j.Wrist));

            for (int i = 0; i < 4; i++)
            {
                mirror[bones.LegLeft[i]] = bones.LegRight[i];
                mirror[bones.LegRight[i]] = bones.LegLeft[i];
            }
            for (int i = 0; i < 3; i++)
            {
                mirror[bones.ArmLeft[i]] = bones.ArmRight[i];
                mirror[bones.ArmRight[i]] = bones.ArmLeft[i];
            }

            bones.Mirror = mirror.ToArray();
            return new Skeleton(list);
        }

        private static Vec3 MirrorZ(Vec3 v) => new Vec3(v.X, v.Y, -v.Z);

        // The point on the underside of the upper skull at fraction t along it — the line the
        // mouth closes against.
        private static Vec3 SampleJawline(DinosaurProfile p, Joints j, float t)
        {
            var skull = j.Skull;

            for (int i = 0; i < skull.Length - 1; i++)
            {
                if (t > skull[i + 1].T && i + 2 < skull.Length) continue;

                float span = skull[i + 1].T - skull[i].T;
                float local = span <= 1e-6f ? 0f : (t - skull[i].T) / span;

                var centre = Vec3.Lerp(skull[i].Centre, skull[i + 1].Centre, local);
                float down = skull[i].Down + (skull[i + 1].Down - skull[i].Down) * local;
                return centre + new Vec3(0f, -p.SkullDepth * down, 0f);
            }

            return skull[skull.Length - 1].Centre;
        }

        private static Vec3 SampleTail(Joints j, float t)
        {
            float scaled = t * (TailStationCount - 1);
            int index = (int)scaled;
            if (index >= TailStationCount - 1) return j.TailPath[TailStationCount - 1];
            return Vec3.Lerp(j.TailPath[index], j.TailPath[index + 1], scaled - index);
        }

        // ---------------------------------------------------------------------------------
        // Geometry
        // ---------------------------------------------------------------------------------

        // Tail tip through to the snout as one unbroken sweep. Building the trunk, neck and
        // skull as separate pieces would leave seams that no amount of skinning hides — the
        // throat and the base of the tail are exactly where the biggest bends happen.
        private static void BuildBody(MeshBuffer mesh, DinosaurProfile p, Joints j, DinosaurBones b, int segments)
        {
            var stations = new List<LoftStation>();
            float v = 0f;

            // Tail, tip first.
            for (int i = TailStationCount - 1; i >= 1; i--)
            {
                float t = (float)i / (TailStationCount - 1);
                float taper = (float)Math.Pow(1f - t, 0.80f);
                float radius = Math.Max(p.TailBaseRadius * taper, 0.006f);

                // Deeper than wide near the base — a theropod tail is a slab of muscle, not a
                // rope, and a circular section makes it read as one.
                BoneSpan(b, t, out int boneA, out int boneB, out float weight);
                stations.Add(new LoftStation(j.TailPath[i],
                    radius * 0.80f, radius * 1.05f, radius * 1.00f, 2.4f, v, boneA, boneB, weight));
                v += 0.035f;
            }

            // Trunk. The pelvis is the widest point on the animal — it carries the thigh
            // muscle — the waist pinches behind the ribs, and the ribcage is the deepest.
            stations.Add(new LoftStation(j.Pelvis,
                p.TorsoWidth * 1.06f, p.BackDepth * 0.90f, p.BellyDepth * 0.72f, 2.7f, v += 0.05f,
                b.Hips, b.Tail[0], 0.78f));

            stations.Add(new LoftStation(j.Waist,
                p.TorsoWidth * 0.90f, p.BackDepth * 0.96f, p.BellyDepth * 0.86f, 2.5f, v += 0.05f,
                b.Spine, b.Hips, 0.55f));

            stations.Add(new LoftStation(j.Ribcage,
                p.TorsoWidth, p.BackDepth, p.BellyDepth, 2.4f, v += 0.05f,
                b.Spine, b.Chest, 0.60f));

            stations.Add(new LoftStation(j.Chest,
                p.TorsoWidth * 0.88f, p.BackDepth * 0.94f, p.BellyDepth * 0.88f, 2.3f, v += 0.04f,
                b.Chest, b.Spine, 0.80f));

            stations.Add(new LoftStation(j.Shoulder,
                p.TorsoWidth * 0.68f, p.BackDepth * 0.84f, p.BellyDepth * 0.58f, 2.2f, v += 0.04f,
                b.Chest, b.NeckLow, 0.60f));

            // Neck.
            stations.Add(new LoftStation(j.NeckLow,
                p.NeckRadius * 1.28f, p.NeckRadius * 1.24f, p.NeckRadius * 1.34f, 2.1f, v += 0.05f,
                b.NeckLow, b.Chest, 0.75f));

            stations.Add(new LoftStation(j.NeckMid,
                p.NeckRadius * 1.02f, p.NeckRadius, p.NeckRadius * 1.06f, 2f, v += 0.05f,
                b.NeckLow, b.NeckHigh, 0.45f));

            stations.Add(new LoftStation(j.NeckTop,
                p.NeckRadius * 0.94f, p.NeckRadius * 0.92f, p.NeckRadius * 0.98f, 2f, v += 0.04f,
                b.NeckHigh, b.Head, 0.70f));

            // Skull, straight off the shared section table. Squareness sits near 2.5, not 3:
            // the cheeks really are flat, but a 14-segment ring at exponent 3 has visible
            // corners, and corners on a face read as damage.
            for (int i = 0; i < j.Skull.Length; i++)
            {
                var s = j.Skull[i];
                stations.Add(new LoftStation(s.Centre,
                    p.SkullWidth * s.Width, p.SkullDepth * s.Up, p.SkullDepth * s.Down, s.Squareness,
                    v += 0.03f, b.Head, i == 0 ? b.NeckHigh : b.Head, i == 0 ? 0.80f : 1f));
            }

            Loft.Build(mesh, stations, segments, new Vec3(0f, 1f, 0f));
        }

        // Which two tail bones a station at fraction t sits between, and how much it leans on
        // the first. Smooth spans rather than hard assignment are what stop the tail creasing
        // into visible facets when it sways.
        private static void BoneSpan(DinosaurBones b, float t, out int boneA, out int boneB, out float weightA)
        {
            float scaled = t * b.Tail.Length;
            int index = (int)scaled;
            if (index >= b.Tail.Length) index = b.Tail.Length - 1;

            boneA = b.Tail[index];
            boneB = index + 1 < b.Tail.Length ? b.Tail[index + 1] : b.Tail[index];
            weightA = 1f - (scaled - index) * 0.5f;
        }

        // A separate lower jaw, hinged on its own bone. It exists so the mouth can open — for
        // the roar (section 11) and the death animation — which a single closed head mesh
        // cannot do at any price.
        private static void BuildJaw(MeshBuffer mesh, DinosaurProfile p, Joints j, DinosaurBones b, int segments)
        {
            // The mandible is hung off the skull's own lower edge rather than authored beside
            // it. That single change is what closes the mouth: any independently-placed jaw
            // needs its every station kept in agreement with a skull that is still being tuned,
            // and the moment one drifts the animal turns into a pelican. Here it cannot drift —
            // the jawline *is* the skull's jawline, by construction.
            //
            // Each station sits a hair above that line (`Up`) so the two surfaces interpenetrate
            // slightly and the seam reads as a closed mouth instead of a join.
            var stations = new List<LoftStation>();
            float[] samples = { 0.04f, 0.22f, 0.48f, 0.74f, 0.97f };
            float[] widths = { 0.94f, 0.88f, 0.72f, 0.54f, 0.24f };
            float[] depths = { 1.00f, 0.92f, 0.70f, 0.48f, 0.22f };

            for (int i = 0; i < samples.Length; i++)
            {
                var mouthLine = SampleJawline(p, j, samples[i]);
                stations.Add(new LoftStation(mouthLine,
                    p.SkullWidth * widths[i],
                    // Pushed a third of the skull's depth up *into* the upper jaw. A flush join
                    // leaves a hairline slot that reads as a permanently open mouth; burying
                    // the mandible's top edge turns the same join into a lip.
                    p.SkullDepth * 0.30f,
                    p.JawDepth * depths[i],
                    2.5f, 0.90f + 0.02f * i, b.Jaw, b.Jaw, 1f));
            }

            Loft.Build(mesh, stations, Math.Max(6, segments - 4), new Vec3(0f, 1f, 0f));
        }

        // The dorsal crest, from the back of the skull to the base of the tail. This is the
        // silhouette read: on a 390px-wide phone the dinosaur is a couple of hundred pixels
        // tall, and an unbroken top line is what tells the eye "raptor" at that size.
        private static void BuildCrest(MeshBuffer mesh, DinosaurProfile p, Joints j, DinosaurBones b)
        {
            // Tail-to-head order, so each station's outward direction can be taken from the
            // spine's own tangent. Offsetting along world up instead — which the first pass did
            // — is fine over the back but catastrophic over the neck: the neck is very nearly
            // vertical there, "up" runs *along* it rather than out of it, and the ridge ends up
            // buried in the throat with a few spikes poking through the sides.
            var spine = new[] { j.Pelvis, j.Waist, j.Ribcage, j.Chest, j.Shoulder, j.NeckLow, j.NeckMid, j.NeckTop, j.Occiput };
            var depths = new[]
            {
                p.BackDepth * 0.84f, p.BackDepth * 0.90f, p.BackDepth * 0.94f, p.BackDepth * 0.88f,
                p.BackDepth * 0.78f, p.NeckRadius * 1.12f, p.NeckRadius * 0.90f, p.NeckRadius * 0.80f,
                p.SkullDepth * 0.74f,
            };
            var heights = p.CrestProfile;
            var bones = new[] { b.Hips, b.Spine, b.Spine, b.Chest, b.Chest, b.NeckLow, b.NeckLow, b.NeckHigh, b.Head };

            var stations = new List<BladeStation>(spine.Length);

            for (int i = 0; i < spine.Length; i++)
            {
                var previous = spine[Math.Max(i - 1, 0)];
                var next = spine[Math.Min(i + 1, spine.Length - 1)];
                var tangent = (next - previous).Normalised;

                // Rotate the tangent a quarter turn in the side plane: along the back that is
                // straight up, up the neck it is backwards, and through the shoulder it sweeps
                // smoothly between the two.
                var outward = new Vec3(-tangent.Y, tangent.X, 0f).Normalised;

                float height = p.CrestHeight * heights[i];

                // Rooted below the surface so the ridge grows out of the body rather than
                // balancing on it, and thinning towards its edge — a constant-thickness strip
                // is what made the first pass look like a playing card taped to the spine.
                stations.Add(new BladeStation(
                    spine[i] + outward * (depths[i] - height * 0.50f),
                    spine[i] + outward * (depths[i] + height),
                    1f - (float)i / (spine.Length - 1), bones[i],
                    // Proportional to the ridge's own height, but capped against the body's
                    // width. Without the cap a tall crest scales its thickness with it and a
                    // Spinosaurus's sail comes out as a rounded hump; a sail is a thin fin no
                    // matter how tall it gets.
                    thickness: Math.Min(
                        Math.Max(height * 0.40f, p.CrestHeight * 0.10f),
                        p.TorsoWidth * 0.34f)));
            }

            Primitives.Blade(mesh, stations, p.CrestHeight * 0.20f);
        }

        private static void BuildTailFan(MeshBuffer mesh, DinosaurProfile p, Joints j, DinosaurBones b)
        {
            var stations = new List<BladeStation>();
            int tip = b.Tail[b.Tail.Length - 1];

            for (int i = 0; i < 4; i++)
            {
                float t = 0.72f + 0.09f * i;
                var spine = SampleTail(j, t);
                float fade = 1f - Math.Abs(t - 0.86f) * 4.5f;
                float height = p.TailFeatherLength * Math.Max(0.25f, fade);

                stations.Add(new BladeStation(
                    spine + new Vec3(0f, 0.004f, 0f),
                    spine + new Vec3(-height * 0.25f, height, 0f),
                    i / 3f, tip));
            }

            Primitives.Blade(mesh, stations, p.TailFeatherLength * 0.10f);
        }

        private static void BuildEye(MeshBuffer mesh, DinosaurProfile p, Joints j, DinosaurBones b, DinosaurDetail detail)
        {
            // Sat just proud of the skull wall at the orbit, so the eyeball breaks the silhouette
            // slightly rather than being swallowed by the head — buried eyes were why the first
            // pass had a blank face.
            var centre = j.Orbit + new Vec3(p.SkullLength * 0.02f, p.SkullDepth * 0.22f, p.SkullWidth * 0.86f);

            int rings = detail == DinosaurDetail.High ? 6 : 4;
            int segments = detail == DinosaurDetail.High ? 8 : 6;

            // UV parked on the eye patch of the generated texture — see DinosaurTexture.
            Primitives.Sphere(mesh, centre, p.SkullDepth * 0.28f, rings, segments, b.Head,
                new Vec2(0.06f, 0.94f), flattenZ: 0.70f);

            // A brow ridge over it. Theropod eyes sit under a heavy shelf, and without it the
            // face reads as a lizard's — wide-eyed and harmless. Swept back over the skull as a
            // ridge rather than sitting on it as a bar.
            var browBack = centre + new Vec3(-p.SkullLength * 0.13f, p.SkullDepth * 0.26f, -p.SkullWidth * 0.22f);
            var browPeak = centre + new Vec3(-p.SkullLength * 0.01f, p.SkullDepth * 0.34f, -p.SkullWidth * 0.10f);
            var browFront = centre + new Vec3(p.SkullLength * 0.15f, p.SkullDepth * 0.10f, -p.SkullWidth * 0.16f);

            var stations = new List<LoftStation>
            {
                new LoftStation(browBack, p.SkullWidth * 0.12f, p.SkullDepth * 0.07f, p.SkullDepth * 0.09f, 2.2f, 0.5f, b.Head, b.Head, 1f),
                new LoftStation(browPeak, p.SkullWidth * 0.19f, p.SkullDepth * 0.10f, p.SkullDepth * 0.13f, 2.4f, 0.5f, b.Head, b.Head, 1f),
                new LoftStation(browFront, p.SkullWidth * 0.10f, p.SkullDepth * 0.05f, p.SkullDepth * 0.07f, 2.2f, 0.5f, b.Head, b.Head, 1f),
            };

            Loft.Build(mesh, stations, 6, new Vec3(0f, 1f, 0f));
        }

        private static void BuildLeg(MeshBuffer mesh, DinosaurProfile p, Joints j, int[] leg, int segments, DinosaurDetail detail)
        {
            int thigh = leg[0], shin = leg[1], foot = leg[2], toe = leg[3];

            // Only two segments fewer than the body, not six. The legs are the largest thing on
            // screen after the torso and they are lit side-on, so a coarse ring shows up as flat
            // facets running the length of the thigh — which is what made the first pass look
            // like the animal was wearing armour plates.
            int limbSegments = Math.Max(8, segments - 2);

            // Thigh: the heaviest muscle on the animal. It starts buried inside the flank —
            // begun above and inboard of the hip joint so it merges into the pelvis instead of
            // budding off it as a separate tube, which is what the first pass looked like — and
            // it is more than twice the width of the shank it feeds.
            Loft.Build(mesh, new List<LoftStation>
            {
                // Read the three radii carefully here, because they do not mean on a leg what
                // they mean on the body. The loft frame is carried along the sweep, so for a
                // limb hanging vertically the ring's "up" has rotated onto +X: HalfHeightUp is
                // how far the muscle reaches *forward*, HalfHeightDown how far it reaches
                // *backward*, and HalfWidth is the only one still measuring sideways.
                //
                // Getting that backwards is what put a flat shield behind each haunch — a
                // "1.95 deep" thigh turned out to be a 10cm flap projecting off the back of the
                // hip. Which way round they run is worth stating rather than rediscovering.
                //
                // So: deeper front-to-back than it is wide, bulging rearwards where the caudo-
                // femoral muscle sits, and rounded (squareness 2.1) because this is the one part
                // seen edge-on against the body from the run camera, where a squared section
                // catches the key light as a flat panel.
                new LoftStation(j.Hip + new Vec3(0f, p.LegWidth * 1.70f, -p.LegWidth * 0.18f),
                    p.LegWidth * 0.94f, p.LegWidth * 1.05f, p.LegWidth * 1.55f, 2.1f, 0.10f, thigh, thigh, 1f),
                new LoftStation(j.Hip + new Vec3(-p.LegWidth * 0.12f, p.LegWidth * 0.30f, 0f),
                    p.LegWidth * 1.00f, p.LegWidth * 1.20f, p.LegWidth * 1.62f, 2.1f, 0.16f, thigh, thigh, 1f),
                new LoftStation(Vec3.Lerp(j.Hip, j.Knee, 0.50f),
                    p.LegWidth * 0.92f, p.LegWidth * 1.02f, p.LegWidth * 1.24f, 2.1f, 0.22f, thigh, thigh, 1f),
                new LoftStation(Vec3.Lerp(j.Hip, j.Knee, 0.82f),
                    p.LegWidth * 0.72f, p.LegWidth * 0.78f, p.LegWidth * 0.86f, 2.1f, 0.28f, thigh, thigh, 1f),
                new LoftStation(j.Knee, p.LegWidth * 0.58f, p.LegWidth * 0.62f, p.LegWidth * 0.66f, 2.1f, 0.32f, shin, thigh, 0.55f),
            }, limbSegments, new Vec3(1f, 0f, 0f), capStart: true, capEnd: false);

            // Shank: a drumstick. Bulging just below the knee and tapering hard into a thin
            // ankle is the giveaway that this is a bird's leg rather than a lizard's, and it is
            // where all the visual power of the stride comes from.
            Loft.Build(mesh, new List<LoftStation>
            {
                // Same convention: Down is the rearward reach, so the bulge below the knee is
                // the calf and it belongs behind the bone, not around it.
                new LoftStation(j.Knee, p.LegWidth * 0.58f, p.LegWidth * 0.62f, p.LegWidth * 0.70f, 2.1f, 0.34f, shin, shin, 1f),
                new LoftStation(Vec3.Lerp(j.Knee, j.Ankle, 0.22f), p.LegWidth * 0.66f, p.LegWidth * 0.68f, p.LegWidth * 1.00f, 2.1f, 0.38f, shin, shin, 1f),
                new LoftStation(Vec3.Lerp(j.Knee, j.Ankle, 0.55f), p.LegWidth * 0.46f, p.LegWidth * 0.48f, p.LegWidth * 0.58f, 2.1f, 0.44f, shin, shin, 1f),
                new LoftStation(j.Ankle, p.LegWidth * 0.26f, p.LegWidth * 0.28f, p.LegWidth * 0.30f, 2.2f, 0.52f, foot, shin, 0.60f),
            }, limbSegments, new Vec3(1f, 0f, 0f), capStart: false, capEnd: false);

            // Metatarsus: the long "shin" people mistake for one. Thin and tendon-like.
            Loft.Build(mesh, new List<LoftStation>
            {
                new LoftStation(j.Ankle, p.LegWidth * 0.28f, p.LegWidth * 0.30f, p.LegWidth * 0.30f, 2.4f, 0.54f, foot, foot, 1f),
                new LoftStation(Vec3.Lerp(j.Ankle, j.Ball, 0.55f), p.LegWidth * 0.24f, p.LegWidth * 0.26f, p.LegWidth * 0.26f, 2.6f, 0.62f, foot, foot, 1f),
                new LoftStation(j.Ball, p.LegWidth * 0.30f, p.LegWidth * 0.26f, p.LegWidth * 0.30f, 2.8f, 0.70f, toe, foot, 0.55f),
            }, limbSegments, new Vec3(1f, 0f, 0f), capStart: false, capEnd: true);

            BuildToes(mesh, p, j, toe, limbSegments, detail);
        }

        private static void BuildToes(MeshBuffer mesh, DinosaurProfile p, Joints j, int toe, int segments, DinosaurDetail detail)
        {
            // Three forward toes, splayed. The inner one is held clear of the ground and
            // carries the sickle claw — the one piece of raptor anatomy everybody recognises,
            // and worth its handful of triangles.
            float[] spread = { -0.55f, 0f, 0.55f };
            float[] length = { 0.82f, 1f, 0.78f };

            for (int i = 0; i < spread.Length; i++)
            {
                var tip = j.Ball + new Vec3(
                    p.ToeLength * length[i],
                    (j.ToeTip.Y - j.Ball.Y) * length[i],
                    p.ToeLength * spread[i] * 0.42f);

                // Substantial toes, not twigs. A running theropod's foot is a wide tripod that
                // carries the whole animal, and at the size the run camera shows it the foot is
                // what sells the weight of every stride.
                Loft.Build(mesh, new List<LoftStation>
                {
                    new LoftStation(j.Ball + new Vec3(-p.LegWidth * 0.10f, 0f, p.ToeLength * spread[i] * 0.10f),
                        p.LegWidth * 0.34f, p.LegWidth * 0.30f, p.LegWidth * 0.26f, 2.8f, 0.72f, toe, toe, 1f),
                    new LoftStation(Vec3.Lerp(j.Ball, tip, 0.50f),
                        p.LegWidth * 0.26f, p.LegWidth * 0.24f, p.LegWidth * 0.22f, 2.9f, 0.80f, toe, toe, 1f),
                    new LoftStation(tip,
                        p.LegWidth * 0.16f, p.LegWidth * 0.15f, p.LegWidth * 0.14f, 2.6f, 0.88f, toe, toe, 1f),
                }, Math.Max(6, segments - 4), new Vec3(0f, 1f, 0f));

                if (detail == DinosaurDetail.Low) continue;

                Primitives.Claw(mesh, tip, new Vec3(1f, -0.25f, spread[i] * 0.35f), new Vec3(0f, 0f, 1f),
                    p.ToeLength * 0.30f, p.LegWidth * 0.085f, -0.9f, toe, segments: 5, steps: 3);
            }

            if (detail == DinosaurDetail.Low) return;

            // The raised sickle claw on the inner digit.
            var sickleBase = j.Ball + new Vec3(p.ToeLength * 0.16f, p.LegWidth * 0.34f, -p.ToeLength * 0.30f);
            Primitives.Claw(mesh, sickleBase, new Vec3(0.55f, 0.35f, -0.12f), new Vec3(0f, 0f, 1f),
                p.ToeLength * 0.72f, p.LegWidth * 0.14f, -1.9f, toe, segments: 6, steps: 5);
        }

        private static void BuildArm(MeshBuffer mesh, DinosaurProfile p, Joints j, int[] arm, int segments, bool plumage)
        {
            int upper = arm[0], fore = arm[1], hand = arm[2];
            int limbSegments = Math.Max(5, segments - 6);

            Loft.Build(mesh, new List<LoftStation>
            {
                new LoftStation(j.ArmRoot, p.ArmRadius * 1.25f, p.ArmRadius * 1.30f, p.ArmRadius * 1.25f, 2.3f, 0.12f, upper, upper, 1f),
                new LoftStation(Vec3.Lerp(j.ArmRoot, j.Elbow, 0.5f), p.ArmRadius, p.ArmRadius * 1.05f, p.ArmRadius, 2.2f, 0.18f, upper, upper, 1f),
                new LoftStation(j.Elbow, p.ArmRadius * 0.74f, p.ArmRadius * 0.78f, p.ArmRadius * 0.74f, 2.2f, 0.24f, fore, upper, 0.60f),
            }, limbSegments, new Vec3(1f, 0f, 0f));

            Loft.Build(mesh, new List<LoftStation>
            {
                new LoftStation(j.Elbow, p.ArmRadius * 0.72f, p.ArmRadius * 0.76f, p.ArmRadius * 0.72f, 2.2f, 0.26f, fore, fore, 1f),
                new LoftStation(j.Wrist, p.ArmRadius * 0.48f, p.ArmRadius * 0.50f, p.ArmRadius * 0.48f, 2.2f, 0.34f, hand, fore, 0.55f),
                new LoftStation(j.FingerTip, p.ArmRadius * 0.22f, p.ArmRadius * 0.24f, p.ArmRadius * 0.22f, 2.4f, 0.40f, hand, hand, 1f),
            }, limbSegments, new Vec3(1f, 0f, 0f));

            for (int i = 0; i < 2; i++)
            {
                var from = Vec3.Lerp(j.Wrist, j.FingerTip, 0.55f) + new Vec3(0f, 0f, p.ArmRadius * (i == 0 ? 0.28f : -0.28f));
                Primitives.Claw(mesh, from, new Vec3(0.8f, -0.55f, 0f), new Vec3(0f, 0f, 1f),
                    p.ForearmLength * 0.30f, p.ArmRadius * 0.30f, -1.1f, hand, segments: 5, steps: 3);
            }

            if (!plumage || p.ArmFeatherLength <= 0f) return;

            // Arm feathers, swept back and tucked against the flank. Section 9 asks for
            // believable anatomy and every close relative of this animal had them, but they
            // have to lie along the body: fanned out sideways they read as flat plates bolted
            // to the shoulder, which is exactly how the first pass looked.
            var feathers = new List<BladeStation>();
            for (int i = 0; i <= 4; i++)
            {
                float t = i / 4f;
                var root = Vec3.Lerp(j.Elbow, j.FingerTip, t) + new Vec3(0f, 0f, -p.ArmRadius * 0.35f);
                float length = p.ArmFeatherLength * (0.45f + 0.55f * (float)Math.Sin(Math.PI * (0.30f + 0.55f * t)));
                var tip = root + new Vec3(-length * 0.94f, length * 0.22f, -length * 0.06f);

                feathers.Add(new BladeStation(root, tip, t, i < 2 ? fore : hand,
                    thickness: p.ArmFeatherLength * (0.075f - 0.045f * t)));
            }

            Primitives.Blade(mesh, feathers, p.ArmFeatherLength * 0.06f);
        }

        // ---------------------------------------------------------------------------------
        // Scaling
        // ---------------------------------------------------------------------------------

        private static MeshBuffer Transform(MeshBuffer source, float scale, Vec3 offset)
        {
            var result = new MeshBuffer();

            for (int i = 0; i < source.VertexCount; i++)
                result.AddVertex(source.Positions[i] * scale + offset, source.Uvs[i],
                    source.BoneA[i], source.BoneB[i], source.WeightA[i]);

            for (int t = 0; t < source.Triangles.Count; t += 3)
                result.AddTriangle(source.Triangles[t], source.Triangles[t + 1], source.Triangles[t + 2]);

            return result;
        }

        private static Skeleton Transform(Skeleton source, float scale, Vec3 offset)
        {
            var bones = new List<BoneDefinition>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                var bone = source[i];
                bones.Add(new BoneDefinition(bone.Name, bone.ParentIndex, bone.BindPosition * scale + offset));
            }
            return new Skeleton(bones);
        }
    }
}
