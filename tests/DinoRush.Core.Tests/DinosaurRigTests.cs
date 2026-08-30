using System;
using System.Collections.Generic;
using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    // The dinosaur is generated rather than imported (docs/DECISIONS.md D13), which means there
    // is no artist to notice when a change makes it wrong. These tests are the substitute:
    // every claim the rest of the game relies on the model for is asserted here, so a tweak to
    // a proportion that breaks one fails CI instead of shipping.
    //
    // What is deliberately *not* tested is whether it looks good. That is judged from the
    // renders `tools/AssetForge` writes, and it is not something an assertion can hold.
    [TestFixture]
    public class DinosaurRigTests
    {
        private static readonly PlayerMotorConfig Motor = PlayerMotorConfig.CreateDefault();
        private static readonly DinosaurProfile Profile = DinosaurProfile.Velociraptor();

        private static DinosaurRig Build(DinosaurDetail detail = DinosaurDetail.High) =>
            DinosaurFactory.Create(Profile, Motor, detail);

        // -----------------------------------------------------------------------------------
        // Geometry integrity
        // -----------------------------------------------------------------------------------

        [Test]
        public void MeshIsWellFormed()
        {
            var mesh = Build().Mesh;

            Assert.That(mesh.VertexCount, Is.GreaterThan(0));
            Assert.That(mesh.Triangles.Count % 3, Is.Zero, "Triangle indices must come in threes.");

            foreach (int index in mesh.Triangles)
                Assert.That(index, Is.InRange(0, mesh.VertexCount - 1), "Triangle references a vertex that does not exist.");

            for (int i = 0; i < mesh.VertexCount; i++)
            {
                var p = mesh.Positions[i];
                Assert.That(float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsNaN(p.Z), Is.False,
                    $"Vertex {i} is NaN — a loft frame collapsed.");

                var n = mesh.Normals[i];
                Assert.That(n.Magnitude, Is.EqualTo(1f).Within(0.01f),
                    $"Vertex {i}'s normal is not unit length.");
            }
        }

        [Test]
        public void EveryTriangleHasArea()
        {
            // A zero-area triangle contributes nothing but still costs a vertex fetch, and it
            // poisons the area-weighted normals of the vertices it touches.
            var mesh = Build().Mesh;
            int degenerate = 0;

            for (int t = 0; t < mesh.Triangles.Count; t += 3)
            {
                var a = mesh.Positions[mesh.Triangles[t]];
                var b = mesh.Positions[mesh.Triangles[t + 1]];
                var c = mesh.Positions[mesh.Triangles[t + 2]];

                if (Vec3.Cross(b - a, c - a).Magnitude < 1e-9f) degenerate++;
            }

            Assert.That(degenerate, Is.Zero, $"{degenerate} triangles have no area.");
        }

        [Test]
        public void SkinningWeightsReferenceRealBones()
        {
            var rig = Build();

            for (int i = 0; i < rig.Mesh.VertexCount; i++)
            {
                Assert.That(rig.Mesh.BoneA[i], Is.InRange(0, rig.Skeleton.Count - 1));
                Assert.That(rig.Mesh.BoneB[i], Is.InRange(0, rig.Skeleton.Count - 1));
                Assert.That(rig.Mesh.WeightA[i], Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void SkeletonIsOrderedParentsFirst()
        {
            // Relied on by PosedSkeleton.Resolve, which is a single forward pass with no
            // recursion precisely because this holds.
            var skeleton = Build().Skeleton;

            for (int i = 0; i < skeleton.Count; i++)
                Assert.That(skeleton[i].ParentIndex, Is.LessThan(i));
        }

        [Test]
        public void GenerationIsDeterministic()
        {
            // Two builds of the same profile must be identical. Anything else would mean the
            // model differs between a device and the editor, and between one launch and the
            // next — and would make every other test here a coin toss.
            var a = Build();
            var b = Build();

            Assert.That(b.Mesh.VertexCount, Is.EqualTo(a.Mesh.VertexCount));
            Assert.That(b.Mesh.TriangleCount, Is.EqualTo(a.Mesh.TriangleCount));

            for (int i = 0; i < a.Mesh.VertexCount; i++)
            {
                Assert.That(b.Mesh.Positions[i].X, Is.EqualTo(a.Mesh.Positions[i].X));
                Assert.That(b.Mesh.Positions[i].Y, Is.EqualTo(a.Mesh.Positions[i].Y));
                Assert.That(b.Mesh.Positions[i].Z, Is.EqualTo(a.Mesh.Positions[i].Z));
            }
        }

        // -----------------------------------------------------------------------------------
        // Mobile budget (section 12, section 35)
        // -----------------------------------------------------------------------------------

        [Test]
        public void DetailLevelsDescend()
        {
            int high = Build(DinosaurDetail.High).Mesh.TriangleCount;
            int medium = Build(DinosaurDetail.Medium).Mesh.TriangleCount;
            int low = Build(DinosaurDetail.Low).Mesh.TriangleCount;

            Assert.That(medium, Is.LessThan(high), "LOD1 must be cheaper than LOD0.");
            Assert.That(low, Is.LessThan(medium), "LOD2 must be cheaper than LOD1.");
        }

        [Test]
        public void TriangleBudgetIsRespected()
        {
            // The player is one of very few skinned meshes on screen, but it is also the only
            // one that is always on screen. 4k triangles at LOD0 leaves room for the biome.
            Assert.That(Build(DinosaurDetail.High).Mesh.TriangleCount, Is.LessThan(4000));
            Assert.That(Build(DinosaurDetail.Low).Mesh.TriangleCount, Is.LessThan(1600));
        }

        // -----------------------------------------------------------------------------------
        // Agreement with the collision box
        // -----------------------------------------------------------------------------------

        [Test]
        public void RunningSilhouetteMatchesTheCollisionBox()
        {
            // The whole point of DinosaurFactory's second pass. If these drift apart the player
            // dies to obstacles that visibly missed, or survives ones that visibly hit.
            float running = DinosaurFactory.MeasureSilhouette(
                Build(), PlayerStance.Running, DinosaurFactory.ReferenceSpeed);

            Assert.That(running, Is.EqualTo(Motor.StandingHeightMeters).Within(0.02f),
                "The running silhouette must be as tall as the collision box says the player is.");
        }

        [TestCase("velociraptor")]
        [TestCase("trex")]
        [TestCase("spinosaurus")]
        public void EverySpeciesMatchesTheCollisionBox(string id)
        {
            // The box does not change when the player picks a different dinosaur, so neither may
            // the silhouette. A T-Rex is mostly head and a Spinosaurus is mostly sail, and both
            // put the tallest point somewhere completely different from the raptor's — which is
            // exactly the kind of difference that would otherwise go unnoticed until someone
            // died to a branch that passed a foot over their head.
            Assume.That(DinosaurProfileLibrary.HasProfileFor(id), Is.True);

            var rig = DinosaurFactory.Create(DinosaurProfileLibrary.For(id), Motor);
            float running = DinosaurFactory.MeasureSilhouette(rig, PlayerStance.Running, DinosaurFactory.ReferenceSpeed);

            Assert.That(running, Is.EqualTo(Motor.StandingHeightMeters).Within(0.03f),
                $"{id} stands {running:F3}m against a {Motor.StandingHeightMeters:F2}m collision box.");
        }

        [Test]
        public void EveryCatalogueDinosaurEitherHasItsOwnShapeOrIsKnownNotTo()
        {
            // Guards the honesty of DinosaurProfileLibrary's fallback. It is fine for the
            // quadrupeds to be missing — the rig cannot pose them yet — but it is not fine for
            // that list to drift silently as species are added.
            var withoutShapes = new List<string>();

            foreach (var dinosaur in DinosaurCatalog.All)
                if (!DinosaurProfileLibrary.HasProfileFor(dinosaur.Id))
                    withoutShapes.Add(dinosaur.Id);

            Assert.That(withoutShapes, Is.EquivalentTo(new[] { "triceratops", "stegosaurus", "ankylosaurus" }),
                "The dinosaurs falling back to the starter's shape have changed. Either a quadruped gained a " +
                "profile it cannot be posed with, or a theropod silently lost one.");
        }

        [Test]
        public void RunningPlayerVisiblyHitsOverheadObstacles()
        {
            float running = DinosaurFactory.MeasureSilhouette(
                Build(), PlayerStance.Running, DinosaurFactory.ReferenceSpeed);

            Assert.That(running, Is.GreaterThan(Motor.DuckObstacleBottomMeters),
                "An upright player must reach the underside of an overhead obstacle, or dying to one looks like a bug.");
        }

        [Test]
        public void DuckingPlayerVisiblyPassesUnderOverheadObstacles()
        {
            // This, rather than an exact match to DuckingHeightMeters, is the invariant that
            // actually matters. The collision box's ducking height is a gameplay number; what
            // the player *sees* is whether the animal fits under the thing it just ducked. A
            // bipedal animal cannot halve its own height by squatting, so the duck is a lunge
            // and comes out a little taller than the box — which is harmless, so long as it
            // still clears with margin.
            float ducking = DinosaurFactory.MeasureSilhouette(
                Build(), PlayerStance.Ducking, DinosaurFactory.ReferenceSpeed);

            Assert.That(ducking, Is.LessThan(Motor.DuckObstacleBottomMeters * 0.92f),
                $"Ducking silhouette is {ducking:F2}m against an obstacle whose underside is at " +
                $"{Motor.DuckObstacleBottomMeters:F2}m — too close to call for something the player is told is safe.");
        }

        [Test]
        public void DuckingIsSubstantiallyShorterThanRunning()
        {
            var rig = Build();
            float running = DinosaurFactory.MeasureSilhouette(rig, PlayerStance.Running, DinosaurFactory.ReferenceSpeed);
            float ducking = DinosaurFactory.MeasureSilhouette(rig, PlayerStance.Ducking, DinosaurFactory.ReferenceSpeed);

            Assert.That(ducking, Is.LessThan(running * 0.62f),
                "A duck the player cannot see is a duck the player will not trust.");
        }

        [Test]
        public void SnoutDoesNotReachFarPastTheCollisionBox()
        {
            // The model is 2.3m long and the collision box is 0.8m, so the extremities cannot
            // all sit inside it. The nose is the one that matters: it is how far the head enters
            // an overhead obstacle before the hit registers. At 13 m/s a 0.25m budget is about
            // two frames, which nobody sees.
            var rig = Build();
            float overhang = rig.ForwardExtentMeters - Motor.PlayerHalfWidthMeters;

            Assert.That(overhang, Is.LessThan(0.25f),
                $"The snout reaches {overhang:F2}m past the collision box.");
        }

        // -----------------------------------------------------------------------------------
        // Gait (section 13: "do not allow visible foot sliding")
        // -----------------------------------------------------------------------------------

        [Test]
        public void PlantedFeetDoNotSlide()
        {
            // Section 13's rule, made checkable. The body advances at the run speed while a
            // planted foot travels backwards through it at the same rate, so the foot's
            // position *over the ground* should not change at all while it is down.
            //
            // Measured on the toe, not the ankle: the toe is what touches, and a foot that
            // pivots correctly at the ankle can still scrub its toe along the floor.
            const float speed = 11f;
            var rig = Build();
            var animator = new DinosaurAnimator(rig.Skeleton, rig.Bones);
            var input = new DinosaurAnimationInput { Stance = PlayerStance.Running, SpeedMetersPerSecond = speed };

            for (int i = 0; i < 180; i++) animator.Tick(1f / 60f, input);

            var posed = new PosedSkeleton(rig.Skeleton.Count);
            int toe = rig.Bones.LegLeft[3];

            float travelled = 0f;
            float previousWorldX = float.NaN;
            float worstSlip = 0f;
            float worstPhase = 0f;
            float worstHeight = 0f;
            const float step = 1f / 240f;

            for (int i = 0; i < 240; i++)
            {
                animator.Tick(step, input);
                travelled += speed * step;

                posed.Resolve(rig.Skeleton, animator.Pose);

                // Planted is decided by height, not by phase, so this cannot accidentally test
                // the animator against its own definition of stance.
                //
                // A millimetre, and not a millimetre more. The IK drives a planted toe to
                // exactly its rest height, so true stance frames sit at zero to float
                // precision — whereas the swing approaches the ground continuously, so any
                // looser tolerance sweeps in the frames just before touchdown, where the foot
                // is legitimately travelling forwards at 16 m/s. Those are not slides. The
                // first attempt at this used 8mm and spent its time measuring the swing.
                bool planted = posed.Positions[toe].Y < rig.Skeleton[toe].BindPosition.Y + 0.001f;
                float worldX = travelled + posed.Positions[toe].X;

                if (planted && !float.IsNaN(previousWorldX))
                {
                    float slip = Math.Abs(worldX - previousWorldX);
                    if (slip > worstSlip)
                    {
                        worstSlip = slip;
                        worstPhase = animator.StridePhase;
                        worstHeight = posed.Positions[toe].Y - rig.Skeleton[toe].BindPosition.Y;
                    }
                }

                previousWorldX = planted ? worldX : float.NaN;
            }

            // Per 1/240s frame. At 11 m/s the body covers 46mm in that time, so anything under
            // a couple of millimetres is well below what reads as a slide.
            Assert.That(worstSlip, Is.LessThan(0.004f),
                $"A planted toe moved {worstSlip * 1000f:F1}mm over the ground in one frame " +
                $"(stride phase {worstPhase:F3}, toe {worstHeight * 1000f:F1}mm above rest).");
        }

        [Test]
        public void StrideLengthensWithSpeedInsteadOfOnlyQuickening()
        {
            // A gait that only raises its cadence turns into a scuttle at the top of the
            // difficulty curve, which is where the game is trying to feel fastest.
            var rig = Build();
            Assert.That(StepsPerMetre(rig, 8f), Is.GreaterThan(StepsPerMetre(rig, 13f)),
                "Steps per metre must fall as speed rises — otherwise the stride is not growing.");
        }

        private static float StepsPerMetre(DinosaurRig rig, float speed)
        {
            var animator = new DinosaurAnimator(rig.Skeleton, rig.Bones);
            var input = new DinosaurAnimationInput { Stance = PlayerStance.Running, SpeedMetersPerSecond = speed };

            for (int i = 0; i < 60; i++) animator.Tick(1f / 60f, input);

            int footfalls = 0;
            float distance = 0f;
            const float step = 1f / 120f;

            for (int i = 0; i < 1200; i++)
            {
                animator.Tick(step, input);
                distance += speed * step;
                if (animator.ConsumeFootfall(out _)) footfalls++;
            }

            return footfalls / distance;
        }

        [Test]
        public void FeetLeaveTheGroundWhenAirborne()
        {
            var rig = Build();
            var animator = new DinosaurAnimator(rig.Skeleton, rig.Bones);

            var running = new DinosaurAnimationInput { Stance = PlayerStance.Running, SpeedMetersPerSecond = 11f };
            for (int i = 0; i < 120; i++) animator.Tick(1f / 60f, running);

            var airborne = new DinosaurAnimationInput
            {
                Stance = PlayerStance.Airborne,
                SpeedMetersPerSecond = 11f,
                VerticalVelocity = 6f,
            };
            for (int i = 0; i < 30; i++) animator.Tick(1f / 60f, airborne);

            var posed = new PosedSkeleton(rig.Skeleton.Count);
            posed.Resolve(rig.Skeleton, animator.Pose);

            foreach (int toe in new[] { rig.Bones.LegLeft[3], rig.Bones.LegRight[3] })
                Assert.That(posed.Positions[toe].Y, Is.GreaterThan(rig.Skeleton[toe].BindPosition.Y + 0.10f),
                    "A jumping dinosaur must tuck its legs, not trail them along the floor.");
        }

        [Test]
        public void PoseStaysFiniteAcrossEveryStateTransition()
        {
            // The IK clamps rather than failing, but a NaN anywhere in the chain would silently
            // delete the whole mesh on screen. Walk every stance in turn and check.
            var rig = Build();
            var animator = new DinosaurAnimator(rig.Skeleton, rig.Bones);
            var posed = new PosedSkeleton(rig.Skeleton.Count);

            var stances = new[] { PlayerStance.Running, PlayerStance.Airborne, PlayerStance.Ducking, PlayerStance.Running };

            foreach (var stance in stances)
            {
                foreach (float speed in new[] { 0f, 0.5f, 8f, 13f, 40f })
                {
                    var input = new DinosaurAnimationInput
                    {
                        Stance = stance,
                        SpeedMetersPerSecond = speed,
                        VerticalVelocity = stance == PlayerStance.Airborne ? 5f : 0f,
                        ExtinctionIntensity = 1f,
                    };

                    for (int i = 0; i < 40; i++) animator.Tick(1f / 60f, input);

                    posed.Resolve(rig.Skeleton, animator.Pose);

                    for (int b = 0; b < rig.Skeleton.Count; b++)
                    {
                        var p = posed.Positions[b];
                        Assert.That(float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsNaN(p.Z), Is.False,
                            $"Bone {rig.Skeleton[b].Name} went NaN at {stance}/{speed} m/s.");
                    }
                }
            }
        }

        [Test]
        public void DeathCollapsesTheAnimal()
        {
            var rig = Build();
            var animator = new DinosaurAnimator(rig.Skeleton, rig.Bones);
            var input = new DinosaurAnimationInput { Stance = PlayerStance.Running, SpeedMetersPerSecond = 11f };

            for (int i = 0; i < 120; i++) animator.Tick(1f / 60f, input);

            var posed = new PosedSkeleton(rig.Skeleton.Count);
            posed.Resolve(rig.Skeleton, animator.Pose);
            float aliveHead = posed.Positions[rig.Bones.Head].Y;

            input.Dead = true;
            for (int i = 0; i < 120; i++) animator.Tick(1f / 60f, input);

            posed.Resolve(rig.Skeleton, animator.Pose);

            Assert.That(posed.Positions[rig.Bones.Head].Y, Is.LessThan(aliveHead * 0.7f),
                "Death must drop the head — a corpse that stays upright reads as a frozen frame.");
        }
    }
}
