using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // M3's acceptance check, and the first code that crosses the Core/Unity boundary.
    //
    // It proves three things at once when you press Play:
    //   1. DinoRush.Runtime resolves its reference to the engine-free DinoRush.Core assembly.
    //   2. Core's logic actually executes under Unity's Mono/IL2CPP runtime — not just under
    //      the CoreCLR runtime `dotnet test` uses (see docs/DECISIONS.md D9).
    //   3. The procedural generator and its validator agree in-editor exactly as they do in
    //      the 2000-seed test suite.
    //
    // Uses RuntimeInitializeOnLoadMethod rather than a MonoBehaviour placed in a scene, so it
    // needs no hand-authored scene YAML — see docs/DECISIONS.md D12.
    public static class CoreIntegrationCheck
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void VerifyCoreIsWired()
        {
            var config = RunGenerationConfig.CreateDefault();
            var generator = new SegmentGenerator(config);
            var validator = new RunValidator(config);

            var run = generator.GenerateRun(seed: 1, targetLengthMeters: 1000f);
            var validation = validator.Validate(run);

            if (validation.IsValid)
            {
                Debug.Log(
                    $"[DinoRush] Core is wired up correctly. Generated a {run.TotalLengthMeters:F0}m run " +
                    $"from seed {run.Seed}: {run.Segments.Count} segments, {run.Obstacles.Count} obstacles, " +
                    $"{run.Coins.Count} coins — validator reports no violations.");
            }
            else
            {
                // Should be unreachable: the same generator/validator pair is checked against
                // 2000 seeds in tests/DinoRush.Core.Tests. If this ever fires, it means Unity's
                // runtime disagrees with CoreCLR — most likely a float-precision difference —
                // and the generator's safety margins need revisiting, not this log line.
                Debug.LogError(
                    $"[DinoRush] Core generated an INVALID run under Unity's runtime " +
                    $"({validation.Violations.Count} violations). First: {validation.Violations[0]}");
            }
        }
    }
}
