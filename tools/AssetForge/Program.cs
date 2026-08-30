using DinoRush.Core;

namespace DinoRush.AssetForge;

public static class Program
{
    public static int Main(string[] args)
    {
        string outputDirectory = args.Length > 0
            ? args[0]
            : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "output");

        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var motor = PlayerMotorConfig.CreateDefault();
        var profile = DinosaurProfile.Velociraptor();

        // Every species the library can build, so a proportion change that breaks one of the
        // others cannot hide behind the starter still looking fine.
        foreach (var other in new[] { DinosaurProfile.Tyrannosaurus(), DinosaurProfile.Spinosaurus() })
        {
            var rig = DinosaurFactory.Create(other, motor);
            ObjWriter.Write(Path.Combine(outputDirectory, other.Id + ".obj"), rig.Mesh, other.Id);
            Console.WriteLine(
                $"{other.Id,-26} {rig.Mesh.VertexCount,6} verts  {rig.Mesh.TriangleCount,6} tris  " +
                $"height {rig.StandingHeightMeters:F3}m  forward {rig.ForwardExtentMeters:F3}m");
        }

        DinosaurRig high = null;

        foreach (var detail in new[] { DinosaurDetail.High, DinosaurDetail.Medium, DinosaurDetail.Low })
        {
            var rig = DinosaurFactory.Create(profile, motor, detail);
            high ??= rig;

            string name = $"{profile.Id}_{detail.ToString().ToLowerInvariant()}";
            ObjWriter.Write(Path.Combine(outputDirectory, name + ".obj"), rig.Mesh, name);

            Console.WriteLine(
                $"{name,-26} {rig.Mesh.VertexCount,6} verts  {rig.Mesh.TriangleCount,6} tris  " +
                $"height {rig.StandingHeightMeters:F3}m  " +
                $"forward {rig.ForwardExtentMeters:F3}m  rear {rig.RearExtentMeters:F3}m  " +
                $"{rig.Skeleton.Count} bones");
        }

        ExportGait(high, outputDirectory, "run", speed: 11f, frames: 8);
        ExportStance(high, outputDirectory, motor);

        Console.WriteLine($"\nWritten to {outputDirectory}");
        return 0;
    }

    // A full stride, sampled evenly. Rendered as a filmstrip this is the only way to judge a
    // gait short of running the game: the phase relationship between the legs, the tail's lag
    // and whether the body bobs on the right beat are all invisible in a single frame.
    private static void ExportGait(DinosaurRig rig, string directory, string name, float speed, int frames)
    {
        var animator = new DinosaurAnimator(rig.Skeleton, rig.Bones);
        var input = new DinosaurAnimationInput
        {
            Stance = PlayerStance.Running,
            SpeedMetersPerSecond = speed,
        };

        // Settled first, so the smoothed action weights are at their steady state rather than
        // still easing in from the reset.
        for (int i = 0; i < 120; i++) animator.Tick(1f / 60f, input);

        var posed = new PosedSkeleton(rig.Skeleton.Count);

        for (int f = 0; f < frames; f++)
        {
            posed.Resolve(rig.Skeleton, animator.Pose);
            ObjWriter.Write(Path.Combine(directory, $"{name}_{f:00}.obj"), Skin(rig, posed), $"{name}_{f:00}");

            // One stride split evenly across the frames requested.
            float stridePerFrame = 1f / frames;
            float distance = stridePerFrame * (1.05f + 0.30f * speed);
            animator.Tick(distance / speed, input);
        }

        Console.WriteLine($"{name,-26} {frames} frames at {speed:F0} m/s");
    }

    // The two silhouettes the collision box makes promises about.
    private static void ExportStance(DinosaurRig rig, string directory, PlayerMotorConfig motor)
    {
        foreach (var (label, stance) in new[]
                 {
                     ("stand", PlayerStance.Running),
                     ("duck", PlayerStance.Ducking),
                 })
        {
            var animator = new DinosaurAnimator(rig.Skeleton, rig.Bones);
            var input = new DinosaurAnimationInput { Stance = stance, SpeedMetersPerSecond = 11f };
            for (int i = 0; i < 240; i++) animator.Tick(1f / 60f, input);

            var posed = new PosedSkeleton(rig.Skeleton.Count);
            posed.Resolve(rig.Skeleton, animator.Pose);
            var mesh = Skin(rig, posed);
            mesh.GetBounds(out var min, out var max);

            ObjWriter.Write(Path.Combine(directory, $"stance_{label}.obj"), mesh, label);

            float allowed = stance == PlayerStance.Ducking
                ? motor.DuckingHeightMeters
                : motor.StandingHeightMeters;

            Console.WriteLine(
                $"{"stance_" + label,-26} silhouette {max.Y - min.Y:F3}m  (box says {allowed:F2}m)");

            // Which bone is holding the silhouette up. Tuning a pose against a single height
            // number is guesswork; naming the offender turns it into a fix.
            int tallest = 0;
            for (int i = 1; i < rig.Skeleton.Count; i++)
                if (posed.Positions[i].Y > posed.Positions[tallest].Y) tallest = i;

            var tail = string.Join(" ", Array.ConvertAll(rig.Bones.Tail,
                b => $"{posed.Positions[b].Y:F2}/{Pitch(animator.Pose.LocalRotations[b]):+0.00;-0.00}"));

            Console.WriteLine(
                $"{"",-26} highest bone {rig.Skeleton[tallest].Name} at {posed.Positions[tallest].Y:F3}m; " +
                $"head {posed.Positions[rig.Bones.Head].Y:F3}m, " +
                $"hips {posed.Positions[rig.Bones.Hips].Y:F3}m, tail [{tail}]");
        }
    }

    // Z-axis angle of a rotation the animator built as a pitch, for diagnostics.
    private static float Pitch(Quat q) => 2f * MathF.Atan2(q.Z, q.W);

    private static MeshBuffer Skin(DinosaurRig rig, PosedSkeleton posed)
    {
        var result = new MeshBuffer();
        var mesh = rig.Mesh;

        for (int i = 0; i < mesh.VertexCount; i++)
        {
            var p = posed.Skin(rig.Skeleton, mesh.Positions[i], mesh.BoneA[i], mesh.BoneB[i], mesh.WeightA[i]);
            result.AddVertex(p, mesh.Uvs[i], mesh.BoneA[i], mesh.BoneB[i], mesh.WeightA[i]);
        }

        for (int t = 0; t < mesh.Triangles.Count; t += 3)
            result.AddTriangle(mesh.Triangles[t], mesh.Triangles[t + 1], mesh.Triangles[t + 2]);

        result.RecalculateNormals();
        return result;
    }
}
