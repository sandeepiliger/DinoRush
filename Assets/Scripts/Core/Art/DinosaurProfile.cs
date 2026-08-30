using System;

namespace DinoRush.Core
{
    // Everything that distinguishes one species from another, as numbers.
    //
    // This is the section 50 promise made concrete: a T-Rex is a different set of these values,
    // not a second mesh builder. Proportions are given in *canonical* units where the standing
    // silhouette is 1.0 tall; DinosaurMeshBuilder rescales the finished rig so the silhouette
    // matches PlayerMotorConfig.StandingHeightMeters exactly. That indirection is what keeps
    // "the model is as tall as the hitbox says it is" true by construction rather than by
    // repeated hand-tuning every time a proportion changes.
    public sealed class DinosaurProfile
    {
        public string Id { get; }
        public string DisplayName { get; }

        // --- Trunk ---------------------------------------------------------------------
        // Hip height as a fraction of total height, and how deep the ribcage hangs below the
        // spine. A theropod carries its mass in front of the hips, so the belly is the lowest
        // point of the trunk and sits well forward of the pelvis.
        public float HipHeight { get; }
        public float TorsoLength { get; }
        public float TorsoWidth { get; }
        public float BellyDepth { get; }
        public float BackDepth { get; }

        // --- Tail ----------------------------------------------------------------------
        public float TailLength { get; }
        public float TailBaseRadius { get; }
        public float TailRise { get; }
        public int TailBones { get; }

        // --- Neck and head --------------------------------------------------------------
        public float NeckLength { get; }
        public float NeckRadius { get; }
        public float NeckCurve { get; }
        public float SkullLength { get; }
        public float SkullDepth { get; }
        public float SkullWidth { get; }
        public float SnoutDrop { get; }
        public float JawDepth { get; }

        // --- Limbs ----------------------------------------------------------------------
        // Digitigrade hind limb: femur down-and-forward, tibia down-and-back to a high ankle,
        // metatarsus down-and-forward again. Getting that zig-zag right is most of what makes
        // a running theropod read as a bird rather than as a lizard on two legs.
        public float FemurLength { get; }
        public float TibiaLength { get; }
        public float MetatarsusLength { get; }
        public float ToeLength { get; }
        public float LegWidth { get; }
        public float HipSpacing { get; }
        public float KneeForward { get; }
        public float AnkleBack { get; }

        public float ArmLength { get; }
        public float ForearmLength { get; }
        public float ArmRadius { get; }

        // --- Plumage --------------------------------------------------------------------
        public float CrestHeight { get; }
        public float ArmFeatherLength { get; }
        public float TailFeatherLength { get; }

        // How the dorsal ridge's height varies from pelvis to crown, as nine multipliers of
        // CrestHeight. This has to be data and not a constant curve, because it is the whole
        // difference between a raptor's neck ruff and a Spinosaurus's sail — same geometry,
        // same code, opposite distribution.
        public float[] CrestProfile { get; }

        private static readonly float[] NeckRidge =
            { 0.22f, 0.32f, 0.40f, 0.50f, 0.66f, 1.00f, 1.25f, 1.15f, 0.55f };

        // --- Palette --------------------------------------------------------------------
        public PaletteColor BackColour { get; }
        public PaletteColor FlankColour { get; }
        public PaletteColor BellyColour { get; }
        public PaletteColor StripeColour { get; }
        public PaletteColor CrestColour { get; }
        public PaletteColor ClawColour { get; }
        public PaletteColor EyeColour { get; }

        public DinosaurProfile(
            string id, string displayName,
            float hipHeight, float torsoLength, float torsoWidth, float bellyDepth, float backDepth,
            float tailLength, float tailBaseRadius, float tailRise, int tailBones,
            float neckLength, float neckRadius, float neckCurve,
            float skullLength, float skullDepth, float skullWidth, float snoutDrop, float jawDepth,
            float femurLength, float tibiaLength, float metatarsusLength, float toeLength,
            float legWidth, float hipSpacing, float kneeForward, float ankleBack,
            float armLength, float forearmLength, float armRadius,
            float crestHeight, float armFeatherLength, float tailFeatherLength,
            PaletteColor backColour, PaletteColor flankColour, PaletteColor bellyColour,
            PaletteColor stripeColour, PaletteColor crestColour, PaletteColor clawColour,
            PaletteColor eyeColour,
            float[] crestProfile = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id is required.", nameof(id));
            if (tailBones < 2) throw new ArgumentOutOfRangeException(nameof(tailBones));

            CrestProfile = crestProfile ?? NeckRidge;
            if (CrestProfile.Length != NeckRidge.Length)
                throw new ArgumentException($"A crest profile needs exactly {NeckRidge.Length} entries.", nameof(crestProfile));

            Id = id;
            DisplayName = displayName;
            HipHeight = hipHeight;
            TorsoLength = torsoLength;
            TorsoWidth = torsoWidth;
            BellyDepth = bellyDepth;
            BackDepth = backDepth;
            TailLength = tailLength;
            TailBaseRadius = tailBaseRadius;
            TailRise = tailRise;
            TailBones = tailBones;
            NeckLength = neckLength;
            NeckRadius = neckRadius;
            NeckCurve = neckCurve;
            SkullLength = skullLength;
            SkullDepth = skullDepth;
            SkullWidth = skullWidth;
            SnoutDrop = snoutDrop;
            JawDepth = jawDepth;
            FemurLength = femurLength;
            TibiaLength = tibiaLength;
            MetatarsusLength = metatarsusLength;
            ToeLength = toeLength;
            LegWidth = legWidth;
            HipSpacing = hipSpacing;
            KneeForward = kneeForward;
            AnkleBack = ankleBack;
            ArmLength = armLength;
            ForearmLength = forearmLength;
            ArmRadius = armRadius;
            CrestHeight = crestHeight;
            ArmFeatherLength = armFeatherLength;
            TailFeatherLength = tailFeatherLength;
            BackColour = backColour;
            FlankColour = flankColour;
            BellyColour = bellyColour;
            StripeColour = stripeColour;
            CrestColour = crestColour;
            ClawColour = clawColour;
            EyeColour = eyeColour;
        }

        // The starter dinosaur (section 8). Proportions are a stylised Velociraptor: shortened
        // snout and a more upright neck than the animal actually had, both deliberate. The
        // snout is the part of the model that reaches furthest past the collision box, so every
        // centimetre of it is a centimetre of nose that visibly enters an obstacle before the
        // hit registers — DinosaurMeshBuilderTests holds that overhang to a budget.
        public static DinosaurProfile Velociraptor() => new DinosaurProfile(
            id: "velociraptor",
            displayName: "Velociraptor",
            // Legs are 60% of total height — longer than the real animal's, and the single
            // most effective stylisation here. It reads as a sprinter, it lifts the torso clear
            // of the ground clutter the camera looks past, and it shortens the neck needed to
            // carry the head to the top of an 1.8m silhouette, which is what stopped the first
            // pass from looking like a llama.
            hipHeight: 0.600f,
            torsoLength: 0.285f,
            torsoWidth: 0.108f,
            bellyDepth: 0.150f,
            backDepth: 0.098f,
            tailLength: 0.780f,
            tailBaseRadius: 0.086f,
            tailRise: 0.075f,
            tailBones: 5,
            neckLength: 0.220f,
            neckRadius: 0.052f,
            neckCurve: 0.048f,
            // Length-to-depth near 2.7. The first pass ran 4.2 and produced a stork: past
            // roughly 3.5 a theropod muzzle stops reading as a jaw and starts reading as a beak,
            // and no amount of work on the mandible rescues it.
            skullLength: 0.192f,
            skullDepth: 0.072f,
            skullWidth: 0.052f,
            snoutDrop: 0.055f,
            jawDepth: 0.040f,
            // Segment lengths and joint offsets together decide how *bent* the leg is at rest,
            // and that turns out to matter more than any of them individually. The first pass
            // stood with the leg at 94% of full extension, which left the IK nothing to give:
            // any stride worth the name over-extended it, the knees locked straight, and the
            // hips had to sink to compensate — which is why the running silhouette came out
            // 13cm shorter than the collision box.
            //
            // Deepening the zig-zag (a knee well forward, an ankle well back) buys length
            // without buying height: same joint positions, same standing height, but the leg is
            // now at 83% extension with real fold in reserve.
            femurLength: 0.281f,
            tibiaLength: 0.257f,
            metatarsusLength: 0.174f,
            toeLength: 0.108f,
            legWidth: 0.054f,
            hipSpacing: 0.078f,
            kneeForward: 0.155f,
            ankleBack: 0.155f,
            armLength: 0.100f,
            forearmLength: 0.095f,
            armRadius: 0.023f,
            // A low ridge, not a sail. At the height the first pass used it read as a fin
            // stuck on the neck, which is worse than no crest at all.
            crestHeight: 0.024f,
            armFeatherLength: 0.088f,
            // Zero disables the tail fan. Drawn as a flat blade it read as a beaver's paddle
            // from every angle the run camera uses, and a plain tapered tail is both truer to
            // the silhouette and cheaper. The code path stays for a species that wants one.
            tailFeatherLength: 0f,
            // Section 9 asks for believable materials, not cartoon colour. A dark olive dorsal
            // fading to a pale sand belly is countershading — the pattern nearly every real
            // ground-running animal wears — with a rust crest for the silhouette read that a
            // 390px-wide phone screen needs.
            backColour: new PaletteColor(0.243f, 0.239f, 0.169f),
            flankColour: new PaletteColor(0.400f, 0.353f, 0.235f),
            bellyColour: new PaletteColor(0.702f, 0.639f, 0.494f),
            stripeColour: new PaletteColor(0.157f, 0.145f, 0.106f),
            crestColour: new PaletteColor(0.639f, 0.286f, 0.129f),
            clawColour: new PaletteColor(0.153f, 0.129f, 0.114f),
            eyeColour: new PaletteColor(0.898f, 0.686f, 0.192f));

        // Everything below this line is the section 50 claim being cashed in: two more animals,
        // recognisably different in silhouette, and not one line of new geometry code between
        // them. Both are theropods, which is why they fit — the rig has two weight-bearing legs.
        // See DinosaurProfileLibrary for why the catalogue's quadrupeds are not here.

        // Heavy. Short thick neck, enormous head, vestigial arms, a tail like a counterweight,
        // and legs proportionally shorter than the raptor's so it reads as mass rather than
        // speed even while running at the same pace.
        public static DinosaurProfile Tyrannosaurus() => new DinosaurProfile(
            id: "trex",
            displayName: "T-Rex",
            hipHeight: 0.520f,
            torsoLength: 0.340f,
            torsoWidth: 0.145f,
            bellyDepth: 0.185f,
            backDepth: 0.125f,
            tailLength: 0.900f,
            tailBaseRadius: 0.125f,
            tailRise: 0.055f,
            tailBones: 5,
            neckLength: 0.160f,
            neckRadius: 0.085f,
            neckCurve: 0.050f,
            skullLength: 0.300f,
            skullDepth: 0.115f,
            skullWidth: 0.082f,
            snoutDrop: 0.050f,
            jawDepth: 0.060f,
            femurLength: 0.244f,
            tibiaLength: 0.224f,
            metatarsusLength: 0.152f,
            toeLength: 0.130f,
            legWidth: 0.082f,
            hipSpacing: 0.095f,
            kneeForward: 0.140f,
            ankleBack: 0.140f,
            armLength: 0.055f,
            forearmLength: 0.045f,
            armRadius: 0.020f,
            crestHeight: 0.014f,
            armFeatherLength: 0f,
            tailFeatherLength: 0f,
            backColour: new PaletteColor(0.271f, 0.235f, 0.196f),
            flankColour: new PaletteColor(0.400f, 0.333f, 0.267f),
            bellyColour: new PaletteColor(0.643f, 0.573f, 0.463f),
            stripeColour: new PaletteColor(0.169f, 0.137f, 0.114f),
            crestColour: new PaletteColor(0.478f, 0.243f, 0.176f),
            clawColour: new PaletteColor(0.129f, 0.114f, 0.102f),
            eyeColour: new PaletteColor(0.831f, 0.408f, 0.145f));

        // Long, low and narrow, with a crocodilian muzzle — and the sail, which is the entire
        // reason CrestProfile is data. Same ridge geometry as the raptor's neck ruff, inverted:
        // tallest over the back, nothing on the neck.
        public static DinosaurProfile Spinosaurus() => new DinosaurProfile(
            id: "spinosaurus",
            displayName: "Spinosaurus",
            hipHeight: 0.550f,
            torsoLength: 0.300f,
            torsoWidth: 0.100f,
            bellyDepth: 0.135f,
            backDepth: 0.085f,
            tailLength: 0.900f,
            tailBaseRadius: 0.085f,
            tailRise: 0.040f,
            tailBones: 5,
            neckLength: 0.240f,
            neckRadius: 0.055f,
            neckCurve: 0.060f,
            skullLength: 0.260f,
            skullDepth: 0.052f,
            skullWidth: 0.040f,
            snoutDrop: 0.060f,
            jawDepth: 0.032f,
            femurLength: 0.259f,
            tibiaLength: 0.235f,
            metatarsusLength: 0.158f,
            toeLength: 0.115f,
            legWidth: 0.058f,
            hipSpacing: 0.082f,
            kneeForward: 0.145f,
            ankleBack: 0.145f,
            armLength: 0.115f,
            forearmLength: 0.105f,
            armRadius: 0.026f,
            crestHeight: 0.320f,
            armFeatherLength: 0f,
            tailFeatherLength: 0f,
            backColour: new PaletteColor(0.208f, 0.235f, 0.259f),
            flankColour: new PaletteColor(0.310f, 0.341f, 0.361f),
            bellyColour: new PaletteColor(0.596f, 0.612f, 0.596f),
            stripeColour: new PaletteColor(0.129f, 0.149f, 0.169f),
            crestColour: new PaletteColor(0.639f, 0.243f, 0.208f),
            clawColour: new PaletteColor(0.141f, 0.137f, 0.129f),
            eyeColour: new PaletteColor(0.918f, 0.780f, 0.290f),
            // Pelvis to crown: the sail rises over the hips, peaks across the ribcage, and is
            // gone by the shoulders.
            crestProfile: new[] { 0.62f, 1.00f, 0.96f, 0.66f, 0.26f, 0.09f, 0.06f, 0.05f, 0.04f });
    }
}
