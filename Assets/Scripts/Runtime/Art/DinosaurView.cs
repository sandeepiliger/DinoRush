using System.Collections.Generic;
using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // The Unity side of the generated dinosaur: uploads the Core-built mesh as a skinned mesh,
    // builds the bone hierarchy, and pushes a pose into it every frame.
    //
    // Deliberately thin. Everything that decides what the animal looks like or how it moves is
    // in Core, where it is tested; this class owns only the things that genuinely need an
    // engine — GameObjects, a Mesh, a material and an LODGroup.
    public sealed class DinosaurView : MonoBehaviour
    {
        private DinosaurRig _rig;
        private DinosaurAnimator _animator;
        private Transform[] _bones;
        private Transform _rootBone;
        private Vec3 _rootBindPosition;

        public DinosaurAnimator Animator => _animator;
        public DinosaurRig Rig => _rig;

        // Where the head is right now, in world space. Used to aim effects at the animal rather
        // than at its transform origin, which is down between its feet.
        public Vector3 HeadPosition =>
            _bones != null ? _bones[_rig.Bones.Head].position : transform.position;

        public static DinosaurView Create(Transform parent, DinosaurProfile profile, PlayerMotorConfig motor)
        {
            var go = new GameObject($"Dinosaur ({profile.DisplayName})");
            go.transform.SetParent(parent, worldPositionStays: false);

            var view = go.AddComponent<DinosaurView>();
            view.Build(profile, motor);
            return view;
        }

        private void Build(DinosaurProfile profile, PlayerMotorConfig motor)
        {
            // LOD0 defines the rig; the cheaper levels are generated against the same skeleton
            // so all three renderers can share one set of bone transforms and one pose update.
            _rig = DinosaurFactory.Create(profile, motor, DinosaurDetail.High);
            _animator = new DinosaurAnimator(_rig.Skeleton, _rig.Bones);

            CreateBones();

            var material = CreateMaterial(profile);

            var lods = new List<LOD>();
            var details = new[] { DinosaurDetail.High, DinosaurDetail.Medium, DinosaurDetail.Low };

            // Screen-relative heights at which each level takes over. The run camera holds a
            // near-constant distance, so on its own this would never switch — the reason it is
            // here is QualitySettings.lodBias, which lets a low-end device drop the whole model
            // a level globally (section 36's quality tiers) without any other code changing.
            var thresholds = new[] { 0.35f, 0.14f, 0.02f };

            for (int i = 0; i < details.Length; i++)
            {
                var rig = i == 0 ? _rig : DinosaurMeshBuilder.Build(profile, _rig.StandingHeightMeters, details[i]);
                var renderer = CreateRenderer(details[i].ToString(), rig, material);
                lods.Add(new LOD(thresholds[i], new Renderer[] { renderer }));
            }

            var group = gameObject.AddComponent<LODGroup>();
            group.SetLODs(lods.ToArray());
            group.RecalculateBounds();
        }

        private void CreateBones()
        {
            var skeleton = _rig.Skeleton;
            _bones = new Transform[skeleton.Count];

            for (int i = 0; i < skeleton.Count; i++)
            {
                var bone = new GameObject(skeleton[i].Name).transform;
                bone.SetParent(i == 0 ? transform : _bones[skeleton[i].ParentIndex], worldPositionStays: false);
                bone.localPosition = skeleton.LocalBindOffset(i).ToVector3();
                bone.localRotation = Quaternion.identity;
                _bones[i] = bone;
            }

            _rootBone = _bones[0];
            _rootBindPosition = skeleton[0].BindPosition;
        }

        private SkinnedMeshRenderer CreateRenderer(string name, DinosaurRig rig, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);

            var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = BuildMesh(rig);
            renderer.bones = _bones;
            renderer.rootBone = _rootBone;
            renderer.sharedMaterial = material;

            // GPU skinning with a 27-bone rig is cheap; what is not cheap on a mobile GPU is a
            // real-time shadow pass for a model that is always on screen. The dinosaur receives
            // shadows and its contact shadow is drawn separately — see RunGroundShadow.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.skinnedMotionVectors = false;
            renderer.updateWhenOffscreen = false;

            // The bounds never change much — the animal is always about its own size — so a
            // fixed generous box avoids Unity recomputing them from skinned vertices each frame.
            renderer.localBounds = new Bounds(
                new Vector3(rig.ForwardExtentMeters + rig.RearExtentMeters, rig.StandingHeightMeters * 0.5f, 0f) * 0.5f,
                new Vector3(rig.ForwardExtentMeters - rig.RearExtentMeters + 0.6f, rig.StandingHeightMeters + 0.6f, 1.4f));

            return renderer;
        }

        private Mesh BuildMesh(DinosaurRig rig)
        {
            var source = rig.Mesh;
            var mesh = new Mesh { name = $"{rig.Profile.Id}_mesh" };

            var vertices = new Vector3[source.VertexCount];
            var normals = new Vector3[source.VertexCount];
            var uvs = new Vector2[source.VertexCount];
            var weights = new BoneWeight[source.VertexCount];

            for (int i = 0; i < source.VertexCount; i++)
            {
                vertices[i] = source.Positions[i].ToVector3();
                normals[i] = source.Normals[i].ToVector3();
                uvs[i] = new Vector2(source.Uvs[i].X, source.Uvs[i].Y);

                weights[i] = new BoneWeight
                {
                    boneIndex0 = source.BoneA[i],
                    weight0 = source.WeightA[i],
                    boneIndex1 = source.BoneB[i],
                    weight1 = 1f - source.WeightA[i],
                };
            }

            var triangles = new int[source.Triangles.Count];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = source.Triangles[i];

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.boneWeights = weights;
            mesh.triangles = triangles;

            // Bind poses are pure translations because Core builds every bone with an identity
            // bind rotation (see Skeleton) — so the inverse bind matrix is just "move the vertex
            // into the bone's local space".
            var bindPoses = new Matrix4x4[rig.Skeleton.Count];
            for (int i = 0; i < rig.Skeleton.Count; i++)
                bindPoses[i] = Matrix4x4.Translate(-rig.Skeleton[i].BindPosition.ToVector3());

            mesh.bindposes = bindPoses;
            mesh.RecalculateTangents();
            mesh.UploadMeshData(markNoLongerReadable: true);

            return mesh;
        }

        private static Material CreateMaterial(DinosaurProfile profile)
        {
            // Simple Lit, not Lit: the dinosaur has no metal and no per-pixel roughness map, so
            // the full PBR path buys nothing and costs a mobile fragment shader that is roughly
            // twice as expensive (section 36).
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");

            var material = new Material(shader) { name = $"{profile.Id}_material" };
            material.mainTexture = DinosaurTexture.Create(profile);

            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
            if (material.HasProperty("_SpecColor")) material.SetColor("_SpecColor", new Color(0.16f, 0.15f, 0.13f));

            return material;
        }

        // Drives the rig. Called by RunController rather than from Update so the animation
        // advances on exactly the same clock as the run's own simulation — an animator ticking
        // itself would drift a frame ahead or behind depending on script execution order, which
        // is visible as the feet leading or lagging the ground.
        public void Tick(float deltaSeconds, DinosaurAnimationInput input)
        {
            _animator.Tick(deltaSeconds, input);
            Apply();
        }

        public void ResetPose()
        {
            _animator.Reset();
            Apply();
        }

        private void Apply()
        {
            var pose = _animator.Pose;

            _rootBone.localPosition = (_rootBindPosition + pose.RootOffset).ToVector3();

            for (int i = 0; i < _bones.Length; i++)
            {
                var q = pose.LocalRotations[i];
                _bones[i].localRotation = new Quaternion(q.X, q.Y, q.Z, q.W);
            }
        }
    }
}
