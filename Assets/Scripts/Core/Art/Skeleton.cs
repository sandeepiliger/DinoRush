using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    public sealed class BoneDefinition
    {
        public string Name { get; }
        public int ParentIndex { get; }

        // Position in model space with the rig at rest. Bind rotations are deliberately all
        // identity: it makes the inverse bind matrix a pure translation, which is what lets
        // Skin() below be four lines instead of a matrix library.
        public Vec3 BindPosition { get; }

        public BoneDefinition(string name, int parentIndex, Vec3 bindPosition)
        {
            Name = name;
            ParentIndex = parentIndex;
            BindPosition = bindPosition;
        }
    }

    // A bone hierarchy, ordered so a bone's parent always precedes it. That ordering is not
    // cosmetic — it lets forward kinematics run as a single forward pass with no recursion,
    // which is what makes posing cheap enough to do every frame on a phone (section 35).
    public sealed class Skeleton
    {
        private readonly BoneDefinition[] _bones;
        private readonly Dictionary<string, int> _byName;

        public Skeleton(IReadOnlyList<BoneDefinition> bones)
        {
            if (bones == null || bones.Count == 0) throw new ArgumentException("A skeleton needs at least one bone.", nameof(bones));

            _bones = new BoneDefinition[bones.Count];
            _byName = new Dictionary<string, int>(bones.Count);

            for (int i = 0; i < bones.Count; i++)
            {
                var bone = bones[i];
                if (bone.ParentIndex >= i)
                    throw new ArgumentException($"Bone '{bone.Name}' names a parent at index {bone.ParentIndex}, which is not before it.", nameof(bones));

                _bones[i] = bone;
                _byName[bone.Name] = i;
            }
        }

        public int Count => _bones.Length;
        public BoneDefinition this[int index] => _bones[index];
        public IReadOnlyList<BoneDefinition> Bones => _bones;

        public int IndexOf(string name) =>
            _byName.TryGetValue(name, out int index)
                ? index
                : throw new ArgumentOutOfRangeException(nameof(name), $"No bone named '{name}'.");

        public bool TryIndexOf(string name, out int index) => _byName.TryGetValue(name, out index);

        // Local position Unity's Transform hierarchy needs: the offset from the parent's bind
        // position. Set this once when the bone GameObjects are created and thereafter only
        // localRotation changes, which is what a pose is.
        public Vec3 LocalBindOffset(int index)
        {
            var bone = _bones[index];
            return bone.ParentIndex < 0
                ? bone.BindPosition
                : bone.BindPosition - _bones[bone.ParentIndex].BindPosition;
        }
    }

    // A set of local rotations, one per bone, plus a whole-body offset. This is the entire
    // output of the animator: everything the dinosaur does is expressed as rotations of a rig
    // whose rest shape never changes.
    public sealed class Pose
    {
        public Quat[] LocalRotations { get; }
        public Vec3 RootOffset { get; set; }

        public Pose(int boneCount)
        {
            LocalRotations = new Quat[boneCount];
            Reset();
        }

        public void Reset()
        {
            for (int i = 0; i < LocalRotations.Length; i++) LocalRotations[i] = Quat.Identity;
            RootOffset = new Vec3(0f, 0f, 0f);
        }

        public void CopyFrom(Pose other)
        {
            Array.Copy(other.LocalRotations, LocalRotations, LocalRotations.Length);
            RootOffset = other.RootOffset;
        }

        public static void Blend(Pose from, Pose to, float t, Pose result)
        {
            for (int i = 0; i < result.LocalRotations.Length; i++)
                result.LocalRotations[i] = Quat.Nlerp(from.LocalRotations[i], to.LocalRotations[i], t);

            result.RootOffset = Vec3.Lerp(from.RootOffset, to.RootOffset, t);
        }
    }

    // Model-space bone transforms produced by evaluating a pose.
    public sealed class PosedSkeleton
    {
        public Vec3[] Positions { get; }
        public Quat[] Rotations { get; }

        public PosedSkeleton(int boneCount)
        {
            Positions = new Vec3[boneCount];
            Rotations = new Quat[boneCount];
        }

        // Single forward pass — safe because Skeleton guarantees parents come first.
        public void Resolve(Skeleton skeleton, Pose pose)
        {
            for (int i = 0; i < skeleton.Count; i++)
            {
                var bone = skeleton[i];
                var local = pose.LocalRotations[i];

                if (bone.ParentIndex < 0)
                {
                    Rotations[i] = local;
                    Positions[i] = bone.BindPosition + pose.RootOffset;
                }
                else
                {
                    var parentRotation = Rotations[bone.ParentIndex];
                    Rotations[i] = parentRotation * local;
                    Positions[i] = Positions[bone.ParentIndex] +
                                   parentRotation * (bone.BindPosition - skeleton[bone.ParentIndex].BindPosition);
                }
            }
        }

        // Where a vertex bound at `bindPosition` ends up under this pose. Because bind
        // rotations are identity, the usual inverse-bind-matrix product collapses to a
        // subtract, a rotate and an add.
        public Vec3 Skin(Skeleton skeleton, Vec3 bindPosition, int boneA, int boneB, float weightA)
        {
            var a = Positions[boneA] + Rotations[boneA] * (bindPosition - skeleton[boneA].BindPosition);
            if (weightA >= 0.999f || boneA == boneB) return a;

            var b = Positions[boneB] + Rotations[boneB] * (bindPosition - skeleton[boneB].BindPosition);
            return Vec3.Lerp(b, a, weightA);
        }
    }
}
