using System;

namespace DinoRush.Core
{
    // What the animator needs to know about the run this frame. Everything here is already
    // owned by PlayerMotor and RunSession — the animator derives, it never decides.
    public struct DinosaurAnimationInput
    {
        public PlayerStance Stance;
        public float SpeedMetersPerSecond;
        public float FeetHeightMeters;
        public float VerticalVelocity;
        public bool Dead;

        // 0..1 as the world collapses. Drives urgency: a longer stride, a lower head, a tail
        // held harder out behind. Section 5 wants the escalation felt, and the animal's carriage
        // is the closest thing to a face this game has.
        public float ExtinctionIntensity;
    }

    // Procedural animation for the generated rig.
    //
    // No AnimationClips, for the same reason there is no imported mesh: clips would have to be
    // authored in an editor this pipeline does not have, and they would have to be re-authored
    // for every species. Deriving the pose from the run's own state instead means the gait
    // tracks the actual speed continuously — an endless runner accelerates the whole time, and
    // a clip played faster is exactly what foot sliding looks like (section 13 forbids it).
    //
    // Living in Core is what makes the two claims worth making testable: that a planted foot
    // does not move over the ground, and that the ducking silhouette really is short enough to
    // pass under what the collision box says it passes under.
    public sealed class DinosaurAnimator
    {
        // Fraction of the stride each foot spends on the ground. Below 0.5 there is an airborne
        // phase, which is what separates a run from a walk; 0.34 is a committed sprint.
        private const float StanceFraction = 0.32f;

        // Metres of ground covered per complete two-step cycle, as a function of speed. A real
        // animal lengthens its stride *and* quickens its step as it speeds up; holding the
        // stride fixed and only quickening it turns a sprint into a scuttle by 13 m/s.
        //
        // The ceiling is a geometric limit, not a taste one. The foot's fore-aft excursion is
        // StanceFraction of the stride, so the leg has to span sqrt(hip² + (excursion/2)²) at
        // the extremes — past about 3.6m of stride that exceeds what the leg can reach and the
        // IK clamps, which looks exactly like a skater.
        private const float StrideBase = 1.00f;
        private const float StridePerSpeed = 0.20f;
        private const float StrideMin = 1.80f;
        private const float StrideMax = 3.60f;

        private readonly Skeleton _skeleton;
        private readonly DinosaurBones _bones;
        private readonly PosedSkeleton _resolved;
        private readonly Pose _pose;

        private readonly float _femur, _tibia, _metatarsus;
        private readonly float _restHipHeight;
        private readonly float _legReach;

        // Height of the ball-of-foot joint at rest. The IK targets this rather than zero: the
        // toes and claws hang below the joint, and driving the joint itself to ground level
        // buries the foot about 10cm into the floor.
        private readonly float _footBindHeight;
        private readonly float[] _bindAngles;

        private float _phase;
        private float _duck;
        private float _air;
        private float _death;
        private float _landing;
        private bool _wasAirborne;
        private int _lastFootfall = -1;
        private bool _footfallPending;
        private bool _footfallWasLeft;

        public DinosaurAnimator(Skeleton skeleton, DinosaurBones bones)
        {
            _skeleton = skeleton ?? throw new ArgumentNullException(nameof(skeleton));
            _bones = bones ?? throw new ArgumentNullException(nameof(bones));

            _pose = new Pose(skeleton.Count);
            _resolved = new PosedSkeleton(skeleton.Count);

            var hip = skeleton[bones.LegLeft[0]].BindPosition;
            var knee = skeleton[bones.LegLeft[1]].BindPosition;
            var ankle = skeleton[bones.LegLeft[2]].BindPosition;
            var ball = skeleton[bones.LegLeft[3]].BindPosition;

            _femur = (knee - hip).Magnitude;
            _tibia = (ankle - knee).Magnitude;
            _metatarsus = (ball - ankle).Magnitude;
            _legReach = _femur + _tibia + _metatarsus;
            _restHipHeight = hip.Y;
            _footBindHeight = ball.Y;

            // Rest direction of every leg segment, so a solved angle can be turned back into the
            // local rotation the pose actually stores.
            _bindAngles = new[]
            {
                Angle(knee - hip),
                Angle(ankle - knee),
                Angle(ball - ankle),
            };
        }

        public Pose Pose => _pose;

        // Where in the stride the animal is, 0..1. Footstep dust and audio hang off this.
        public float StridePhase => _phase;

        // True once per footfall. Reading it clears it, so a caller cannot miss one and cannot
        // fire the same one twice.
        public bool ConsumeFootfall(out bool leftFoot)
        {
            leftFoot = _footfallWasLeft;
            bool pending = _footfallPending;
            _footfallPending = false;
            return pending;
        }

        public void Reset()
        {
            _pose.Reset();
            _phase = 0f;
            _duck = _air = _death = _landing = 0f;
            _wasAirborne = false;
            _lastFootfall = -1;
            _footfallPending = false;
        }

        public void Tick(float deltaSeconds, DinosaurAnimationInput input)
        {
            if (deltaSeconds <= 0f) return;

            bool airborne = input.Stance == PlayerStance.Airborne;
            float stride = Clamp(StrideBase + StridePerSpeed * input.SpeedMetersPerSecond, StrideMin, StrideMax);

            // The phase advances with *distance covered*, not with time. That is the whole
            // no-foot-sliding guarantee in one line: a foot planted at a given phase moves
            // backwards through the body at exactly the speed the body moves forwards.
            if (!airborne && !input.Dead && input.SpeedMetersPerSecond > 0.01f)
            {
                _phase += input.SpeedMetersPerSecond * deltaSeconds / stride;
                _phase -= (float)Math.Floor(_phase);
                RaiseFootfalls();
            }

            // Smoothed action weights. Every transition in this animator is a weight moving,
            // not a clip being swapped, which is why nothing ever pops.
            _duck = Approach(_duck, input.Stance == PlayerStance.Ducking ? 1f : 0f, deltaSeconds, 0.075f);
            _air = Approach(_air, airborne ? 1f : 0f, deltaSeconds, 0.055f);
            _death = Approach(_death, input.Dead ? 1f : 0f, deltaSeconds, 0.22f);

            // A landing is a spike, not a state: the legs absorb the impact over about a fifth
            // of a second and the body is driven down into them. Without it the animal touches
            // down as if the ground were made of glass.
            if (_wasAirborne && !airborne) _landing = 1f;
            _wasAirborne = airborne;
            _landing = Math.Max(0f, _landing - deltaSeconds * 5.5f);

            BuildPose(input, stride);
        }

        private void RaiseFootfalls()
        {
            // The left foot plants at phase 0, the right at 0.5.
            int half = _phase < 0.5f ? 0 : 1;
            if (half == _lastFootfall) return;

            // Suppress the very first transition after a reset, which is not a real footfall.
            if (_lastFootfall >= 0)
            {
                _footfallPending = true;
                _footfallWasLeft = half == 0;
            }

            _lastFootfall = half;
        }

        private void BuildPose(DinosaurAnimationInput input, float stride)
        {
            _pose.Reset();

            float run = (1f - _air) * (1f - _death);
            float urgency = Clamp(input.ExtinctionIntensity, 0f, 1f);

            // ---- Trunk -----------------------------------------------------------------
            // Two vertical oscillations per cycle, one per footfall, lowest at mid-stance.
            float bobAmplitude = _restHipHeight * (0.030f + 0.016f * Normalise(input.SpeedMetersPerSecond, 6f, 14f));
            float bob = -(float)Math.Cos(_phase * 4f * Math.PI) * bobAmplitude * run;

            // Airborne, the hips ride high and the whole animal stretches; landing drives them
            // down hard. Ducking folds them, and because the legs are solved by IK, folding the
            // hips is the *entire* implementation of crouching — the knees follow for free.
            float hipHeight = _restHipHeight
                              + bob
                              + _air * _restHipHeight * 0.045f
                              - _landing * _restHipHeight * 0.130f
                              - _duck * _restHipHeight * 0.575f
                              - _death * _restHipHeight * 0.660f;

            hipHeight = Math.Max(hipHeight, _restHipHeight * 0.22f);

            // Body lean: forward under speed, further forward as the world falls apart.
            //
            // The duck is not a crouch. The collision box shrinks the player to half height,
            // and a bipedal animal simply squatting cannot halve its own silhouette without
            // sitting down. What it *can* do is dive: pitch the spine towards horizontal, throw
            // the neck out in front instead of up, and stretch the tail out level behind as a
            // counterweight. That reads as a committed slide under the obstacle, it is what a
            // real animal running flat-out under a low branch actually does, and it genuinely
            // fits inside the box rather than merely gesturing at it.
            float leanPitch = -(0.09f + 0.07f * Normalise(input.SpeedMetersPerSecond, 6f, 14f) + 0.10f * urgency) * run
                              - _duck * 0.62f
                              + _air * (input.VerticalVelocity > 0f ? -0.16f : 0.13f);

            float gallopPitch = (float)Math.Sin(_phase * 4f * Math.PI) * 0.045f * run;
            float roll = (float)Math.Sin(_phase * 2f * Math.PI) * 0.055f * run;

            // The commanded hip height has to actually move the body, or the leg IK below is
            // solving towards a socket the skeleton never went to — and the feet end up
            // hanging in the air by exactly the difference.
            _pose.RootOffset = new Vec3(0f, hipHeight - _restHipHeight, 0f);
            _pose.LocalRotations[_bones.Hips] = Quat.Pitch(leanPitch + gallopPitch);
            _pose.LocalRotations[_bones.Spine] = Quat.Pitch(0.035f * run - _duck * 0.10f) * Quat.Roll(roll);
            _pose.LocalRotations[_bones.Chest] = Quat.Pitch(0.030f * run - _duck * 0.06f) * Quat.Roll(roll * 0.6f);

            // ---- Neck and head ---------------------------------------------------------
            // Counter-rotated against the trunk so the head stays level while the body pitches
            // and bobs underneath it. Every running animal does this — a head that bobs with
            // the shoulders is the single clearest sign of a rig being animated rather than
            // moving — and here it also keeps the eye readable at 390px wide.
            float neckLift = 0.20f + 0.10f * urgency;
            float headSettle = -(leanPitch + gallopPitch) * 0.85f;

            // Ducking, the neck unfolds forwards rather than upwards: the S straightens out
            // along the body's new near-horizontal axis, putting the head in front of the chest
            // at chest height instead of above it.
            _pose.LocalRotations[_bones.NeckLow] =
                Quat.Pitch(neckLift * run - _duck * 0.78f - _air * 0.10f);
            _pose.LocalRotations[_bones.NeckHigh] =
                Quat.Pitch(-neckLift * 0.55f * run - _duck * 0.10f + _air * 0.12f);
            _pose.LocalRotations[_bones.Head] =
                Quat.Pitch(headSettle + _duck * 0.34f + _air * 0.10f) *
                Quat.Yaw((float)Math.Sin(_phase * 2f * Math.PI) * 0.035f * run);

            // The mouth opens with urgency and hangs open in death.
            _pose.LocalRotations[_bones.Jaw] = Quat.Pitch(-(0.10f + 0.28f * urgency) * run - 0.55f * _death);

            // ---- Tail ------------------------------------------------------------------
            // A travelling wave down the chain rather than one rigid sway. Each bone lags the
            // one before it, so the tail whips and settles instead of swinging like a plank —
            // the lag is what reads as weight.
            // The tail hangs off the hips, so the body's pitch swings it before any of its own
            // rotation applies. That correction has to land almost entirely on the first bone:
            // spread evenly down the chain it arrives too late, the base has already swung up
            // 40 degrees, and the tail arcs over the animal like a scorpion's — which is what
            // pushed the ducking silhouette half a metre past the collision box.
            //
            // Countering fully would look wrong at a run, where a tail carried in line with a
            // leaning back is correct; countering hardly at all is what breaks the duck. So the
            // fraction rises with the crouch.
            float bodyPitch = leanPitch + gallopPitch;
            float baseCounter = -bodyPitch * (0.30f + 0.82f * _duck);

            for (int i = 0; i < _bones.Tail.Length; i++)
            {
                float along = (float)(i + 1) / _bones.Tail.Length;
                float lag = _phase * 2f * (float)Math.PI - along * 1.5f;

                float yaw = (float)Math.Sin(lag) * 0.10f * along * run;
                float lift = 0.030f * (1f - along * 0.4f) * run
                             + _air * 0.10f * (input.VerticalVelocity > 0f ? 1f : -0.6f)
                             - _death * 0.16f * along;

                if (i == 0) lift += baseCounter;

                _pose.LocalRotations[_bones.Tail[i]] = Quat.Pitch(lift) * Quat.Yaw(yaw);
            }

            // ---- Arms ------------------------------------------------------------------
            // Small, tucked, and swinging opposite the leg on the same side.
            for (int side = 0; side < 2; side++)
            {
                var arm = side == 0 ? _bones.ArmLeft : _bones.ArmRight;
                float swing = (float)Math.Sin(_phase * 2f * Math.PI + (side == 0 ? Math.PI : 0.0)) * 0.22f * run;

                _pose.LocalRotations[arm[0]] = Quat.Pitch(swing - _duck * 0.30f + _air * 0.20f);
                _pose.LocalRotations[arm[1]] = Quat.Pitch(-swing * 0.5f - 0.12f * run - _death * 0.4f);
                _pose.LocalRotations[arm[2]] = Quat.Pitch(swing * 0.3f);
            }

            // ---- Death ------------------------------------------------------------------
            // Layered on top rather than folded into the terms above, because dying is the one
            // thing that overrides the gait entirely rather than colouring it. The trunk pitches
            // nose-down and rolls onto one side, the neck folds, and the hips have already been
            // dropped in the height above — doing that here instead would be too late, since the
            // body offset it feeds has been committed.
            if (_death > 0.001f)
            {
                _pose.LocalRotations[_bones.Hips] =
                    Quat.Nlerp(_pose.LocalRotations[_bones.Hips], Quat.Pitch(-0.85f) * Quat.Roll(1.05f), _death);
                _pose.LocalRotations[_bones.Spine] =
                    Quat.Nlerp(_pose.LocalRotations[_bones.Spine], Quat.Pitch(-0.30f) * Quat.Roll(0.35f), _death);
                _pose.LocalRotations[_bones.NeckLow] =
                    Quat.Nlerp(_pose.LocalRotations[_bones.NeckLow], Quat.Pitch(-1.15f), _death);
                _pose.LocalRotations[_bones.NeckHigh] =
                    Quat.Nlerp(_pose.LocalRotations[_bones.NeckHigh], Quat.Pitch(-0.75f), _death);
                _pose.LocalRotations[_bones.Head] =
                    Quat.Nlerp(_pose.LocalRotations[_bones.Head], Quat.Pitch(-0.45f), _death);
            }

            // ---- Legs -------------------------------------------------------------------
            // Resolved once with the trunk posed and the legs still at rest, purely to find out
            // where the hip sockets have ended up. The IK below needs that, and it depends only
            // on the ancestors of the leg — which are all already posed.
            _resolved.Resolve(_skeleton, _pose);

            SolveLeg(_bones.LegLeft, phaseOffset: 0f, stride, hipHeight, input);
            SolveLeg(_bones.LegRight, phaseOffset: 0.5f, stride, hipHeight, input);
        }

        private void SolveLeg(int[] leg, float phaseOffset, float stride, float hipHeight,
            DinosaurAnimationInput input)
        {
            float phase = _phase + phaseOffset;
            phase -= (float)Math.Floor(phase);

            var hipBind = _skeleton[leg[0]].BindPosition;
            var hipNow = _resolved.Positions[leg[0]];

            // Where the hip socket genuinely ended up once the trunk was posed — body offset,
            // lean, gallop pitch and all. Solving against anything else puts the feet where the
            // ground is not.
            var hip = new Vec2(hipNow.X, hipNow.Y);

            FootTarget(phase, stride, input, out float footX, out float footY);

            // Ducking, the feet run further out in front. A crouch deep enough to halve the
            // silhouette folds the leg so hard that the knee becomes the highest point on the
            // animal — measurably so — and reaching forward instead keeps the leg extended and
            // the knee down. It also reads as the lunge the duck is meant to be. A constant
            // offset is safe: it shifts where the foot plants without changing how fast it
            // travels during stance, so nothing slides.
            footX += _duck * 0.30f * _legReach;

            // Airborne the feet stop tracking the ground and tuck under the body — the legs
            // fold on the way up and reach on the way down, which is what makes a jump read as
            // a leap rather than a lift.
            if (_air > 0.001f)
            {
                bool rising = input.VerticalVelocity > 0f;
                float tuckX = hip.X + (rising ? -0.05f : 0.34f) * _legReach;
                float tuckY = hip.Y - (rising ? 0.58f : 0.86f) * _legReach;

                footX = footX * (1f - _air) + tuckX * _air;
                footY = footY * (1f - _air) + tuckY * _air;
            }

            if (_death > 0.001f)
            {
                footX = footX * (1f - _death) + (hip.X - 0.22f * _legReach) * _death;
                footY = footY * (1f - _death) + 0.02f * _death;
            }

            var ball = new Vec2(hipBind.X + footX, footY);

            // --- three-segment bird leg ---------------------------------------------------
            // Solved as a two-link chain to the ankle, with the metatarsus placed first. A
            // theropod's foot posts the metatarsus at an angle that steepens as the leg
            // compresses, and choosing it up front is what turns an under-determined
            // three-link problem into an exact two-link one.
            float dx = hip.X - ball.X;
            float dy = hip.Y - ball.Y;
            float reach = (float)Math.Sqrt(dx * dx + dy * dy);
            float compression = Clamp(1f - reach / _legReach, 0f, 0.55f);

            float toHip = (float)Math.Atan2(dy, dx);
            float post = 0.34f + 1.25f * compression;
            float ankleAngle = toHip + post;

            var ankle = new Vec2(
                ball.X + (float)Math.Cos(ankleAngle) * _metatarsus,
                ball.Y + (float)Math.Sin(ankleAngle) * _metatarsus);

            // Knee forward: a theropod's knee bends the opposite way to the ankle below it,
            // and picking the wrong solution branch produces the single most recognisable
            // rigging error there is.
            var knee = SolveTwoLink(hip, ankle, _femur, _tibia, kneeForward: true);

            float a1 = Angle(knee.X - hip.X, knee.Y - hip.Y);
            float a2 = Angle(ankle.X - knee.X, ankle.Y - knee.Y);
            float a3 = Angle(ball.X - ankle.X, ball.Y - ankle.Y);

            // Local rotations are relative to the parent's, so each segment's world delta has
            // the one above it subtracted back out.
            float hipsPitch = PitchOf(_resolved.Rotations[_skeleton[leg[0]].ParentIndex]);
            float d1 = a1 - _bindAngles[0];
            float d2 = a2 - _bindAngles[1];
            float d3 = a3 - _bindAngles[2];

            _pose.LocalRotations[leg[0]] = Quat.Pitch(d1 - hipsPitch);
            _pose.LocalRotations[leg[1]] = Quat.Pitch(d2 - d1);
            _pose.LocalRotations[leg[2]] = Quat.Pitch(d3 - d2);

            // The toes stay flat to the ground through the stance and point down through the
            // swing, so the foot lands toe-first the way a digitigrade animal does.
            bool planted = phase < StanceFraction;
            float toe = planted ? -d3 : -d3 * 0.35f - 0.30f;
            _pose.LocalRotations[leg[3]] = Quat.Pitch(toe);
        }

        // Where this foot should be, relative to its hip socket, at this point in the stride.
        private void FootTarget(float phase, float stride, DinosaurAnimationInput input,
            out float x, out float y)
        {
            // Ground covered while a foot is down. Deriving it from the stride rather than
            // choosing it independently is what makes the plant exact: over a stance lasting
            // StanceFraction of the cycle, the body advances StanceFraction of the stride, so
            // a foot travelling this far backwards through the body stands perfectly still on
            // the ground.
            float travel = stride * StanceFraction;

            if (phase < StanceFraction)
            {
                float t = phase / StanceFraction;
                x = travel * (0.5f - t);
                y = _footBindHeight;
                return;
            }

            // Swing: forward again, lifted in an arc weighted towards the start so the foot
            // snaps up off the ground and reaches out flat, rather than floating symmetrically.
            float u = (phase - StanceFraction) / (1f - StanceFraction);
            x = travel * (u - 0.5f);

            float lift = (float)Math.Sin(Math.PI * Math.Pow(u, 0.72));
            y = _footBindHeight +
                lift * _legReach * (0.16f + 0.06f * Normalise(input.SpeedMetersPerSecond, 6f, 14f));
        }

        private static Vec2 SolveTwoLink(Vec2 root, Vec2 target, float l1, float l2, bool kneeForward)
        {
            float dx = target.X - root.X;
            float dy = target.Y - root.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);

            // Clamped just inside full extension and just outside full fold, so the joint never
            // locks straight (which reads as a stiff leg) and acos never sees an out-of-range
            // argument at the extremes.
            float min = Math.Abs(l1 - l2) + 1e-3f;
            float max = l1 + l2 - 1e-3f;
            float clamped = Clamp(distance, min, max);

            float baseAngle = (float)Math.Atan2(dy, dx);
            float cos = (clamped * clamped + l1 * l1 - l2 * l2) / (2f * clamped * l1);
            float interior = (float)Math.Acos(Clamp(cos, -1f, 1f));

            float angle = kneeForward ? baseAngle - interior : baseAngle + interior;

            return new Vec2(
                root.X + (float)Math.Cos(angle) * l1,
                root.Y + (float)Math.Sin(angle) * l1);
        }

        private static float PitchOf(Quat q) => 2f * (float)Math.Atan2(q.Z, q.W);

        private static float Angle(Vec3 v) => (float)Math.Atan2(v.Y, v.X);
        private static float Angle(float x, float y) => (float)Math.Atan2(y, x);

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;

        private static float Normalise(float value, float from, float to) =>
            Clamp((value - from) / (to - from), 0f, 1f);

        // Framerate-independent exponential approach. Lerping by a fixed fraction per frame
        // instead would make every transition in the game faster on a 120Hz phone than on a
        // 60Hz one.
        private static float Approach(float current, float target, float deltaSeconds, float timeConstant) =>
            current + (target - current) * (1f - (float)Math.Exp(-deltaSeconds / timeConstant));
    }
}
