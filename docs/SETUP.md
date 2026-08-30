# Setup

This repo is developed in two places: a Linux container with no Unity installed (used for the
Unity-free `Core` logic and its tests), and your own Windows machine running the Unity editor
(used for everything that needs rendering, scenes, or a device build). This doc covers both.

## Right now: what exists and what doesn't

M0–M2 built and proved the testable game logic entirely outside the editor — deliberately, see
`docs/DECISIONS.md` D9 and D12. **M3 is complete**: the project opens in Unity 6000.3.23f1 with a
clean console, and Core was confirmed to produce byte-identical output under Unity's runtime and
under `dotnet test` (same seed, same segment/obstacle/coin counts) — which is what makes the
seeded-determinism guarantees in sections 15 and 21 real rather than assumed.

**M4 and M5 are in the repo**: a playable run — tap to jump, swipe down to duck, collide, die,
restart — plus collectible coins, pooled scenery, placeholder audio and a smoothed camera.
See "Playing the run" below.

Two notes on layout that are easy to trip over:

- The .NET solution is `DinoRush.Core.sln`, **not** `DinoRush.sln`. Unity generates
  `<project-folder>.sln` whenever it regenerates project files, so a solution named
  `DinoRush.sln` would be silently overwritten with Unity's own and stop building the Core
  projects. CI invokes the test project directly and never depends on either file.
- `Assets/Scripts/Core/` is engine-free and compiled twice (by Unity and by the standalone
  `src/DinoRush.Core.csproj`); `Assets/Scripts/Runtime/` is Unity-only. Adding a `using
  UnityEngine;` to anything under `Core/` breaks the build immediately and by design.

## Building and testing the Core logic (any machine with the .NET SDK)

```bash
dotnet test tests/DinoRush.Core.Tests
```

That's the whole loop. No Unity, no Android SDK, no license required. This also runs in CI on
every push (see `.github/workflows/`).

If you don't have the .NET SDK: install "​.NET SDK 8.0" from https://dotnet.microsoft.com/download
(Windows: the installer; Linux: your distro's package, e.g. `apt install dotnet-sdk-8.0`).

## M3 — bringing Unity into the repo (Windows, your machine)

This is the one step that has to start on your side, because `ProjectSettings/ProjectSettings.asset`
is a large, editor-version-specific file that shouldn't be hand-written blind.

1. Install **Unity 6000.3.23f1** (Unity 6.3 LTS) via Unity Hub, with the **Android Build
   Support** module (including its Android SDK/NDK/OpenJDK sub-components) checked during
   install.
2. Create a **throwaway** project — Hub → New Project → the "Universal 3D" (URP) template,
   any name, anywhere outside this repo.
3. From that throwaway project, copy **three things** into this repo:

   | Copy from throwaway | To repo | Why |
   |---|---|---|
   | `ProjectSettings/` (whole folder) | repo root | Editor version pin, Player Settings, quality/graphics config |
   | `Packages/` (whole folder) | repo root | `manifest.json` declares URP, Input System and the Test Framework at versions matching this exact editor |
   | everything inside `Assets/` | this repo's `Assets/` | The URP pipeline assets, sample scene and input actions that `ProjectSettings/` points at |

   **Copy the `.meta` files alongside every asset** — they carry the GUIDs. Copying whole
   folders (rather than hand-picked files) picks them up automatically.

   The third row is the one that's easy to miss, and the one that breaks things.
   `ProjectSettings/` does not *contain* the render pipeline — it only *references* it by GUID:
   `GraphicsSettings.asset` and `QualitySettings.asset` point at URP assets living in the
   template's `Assets/Settings/`, and `EditorBuildSettings.asset` points at
   `Assets/Scenes/SampleScene.unity` plus the Input System actions asset. Copy
   `ProjectSettings/` alone and every one of those references dangles — the project opens with
   **no working render pipeline**.

   No collision risk: this repo's `Assets/` contains only `Scripts/`, which the template
   doesn't have, so the two merge cleanly.

4. Delete the throwaway project — it's served its purpose.
5. Open **this repo** as a Unity project (Hub → Add → select the `dinorush` folder). Unity will
   generate `.meta` files for everything under `Assets/Scripts/` and resolve
   `Packages/manifest.json` into `Packages/packages-lock.json`.
6. **Acceptance bar for M3** — both must hold:
   - The console shows no red errors once import finishes.
   - Press **Play**. The console logs a line beginning `[DinoRush] Core is wired up correctly`,
     reporting a generated run's segment/obstacle/coin counts. That comes from
     `Assets/Scripts/Runtime/Bootstrap/CoreIntegrationCheck.cs`, and proves the engine-free
     `Core` assembly is correctly referenced *and* that its procedural generator produces valid
     output under Unity's own runtime — not merely under the runtime `dotnet test` uses.
7. Commit: the copied `ProjectSettings/`, `Packages/` and `Assets/` files, the newly generated
   `.meta` files, and `Packages/packages-lock.json`. Do this in one commit before making any
   other changes — skipping this step is what causes GUID drift later ("The referenced script
   on this Behaviour is missing!").

## Playing the run

Open the project and press **Play** — no scene setup needed. `RunBootstrap` builds the camera,
ground, player, coins, scenery and pools in code on load (D12), so it works from `SampleScene`
or from an empty scene, reusing whatever camera and light the scene already has.

Gold discs are coins. Most sit at running height, but `CoinPattern` segments lay them in an arc
peaking at the jump apex — those have to be jumped for, and are the one thing asking something
of you on an otherwise safe stretch. Collection is an overlap test in Core, not a distance
check, so an arc coin genuinely requires leaving the ground.

| Action | Touch | Editor |
|---|---|---|
| Jump | tap | Space / W / ↑, or left-click |
| Duck | swipe down | S / ↓, or drag down |
| Restart after dying | tap | any key |

Everything on screen is a primitive placeholder, per section 72 — the point of M4 is proving the
run feels right before any art exists. Gold capsule is the player; rust-coloured blocks are
ground obstacles to jump; purple blocks hang overhead and must be ducked under.

A note on tap timing: jump fires on finger *release*, not touch-down. It has to — a swipe-down
starts with a touch-down too, so firing a jump there would make every duck attempt jump first.
Ducking is not delayed: it fires the instant the swipe threshold is crossed.

### What is and isn't verifiable outside the editor

The run's *rules* are all unit-tested in `Core` and run in CI: the jump arc, duck timing,
collision, scoring, speed escalation, and a safety suite proving the tuning is compatible with
the generator's spacing floor (a jump covers 6.4m at top speed against a 7.4m minimum gap, and
clears ground obstacles with 56% headroom). What CI cannot judge is whether it *feels* good —
input latency, camera framing, and jump weight are yours to assess by playing it. Retuning is
just editing `PlayerMotorConfig.CreateDefault()`; the safety tests will fail loudly if a change
makes obstacles unclearable.

After that, Player Settings worth checking against §44 before any build:
- Application ID: `com.iligergames.dinorush`
- Scripting Backend: IL2CPP
- Target Architectures: ARM64 only
- Target API Level: Android 16 (API 36) or "Automatic (highest installed)" — required for new
  app submissions from Aug 31, 2026 per current Play policy
- Render Pipeline Asset: the URP asset (Universal 3D template sets this up automatically)

## One-time git config (Windows, your machine)

`.gitattributes` references a Unity smart-merge driver for scene/prefab/asset YAML that git
itself doesn't know how to configure automatically. Run once, after installing Unity:

```
git config merge.unityyamlmerge.driver "'<path-to-Unity-Editor>/Data/Tools/UnityYAMLMerge' merge -p %O %A %B %A"
git config merge.unityyamlmerge.name "Unity smart merge"
```

`<path-to-Unity-Editor>` is typically `C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor` on
Windows. Without this, a merge conflict in a `.unity`/`.prefab` file falls back to git's default
text merge, which is exactly the fragile YAML-editing scenario `docs/DECISIONS.md` D12 avoids
everywhere else — so don't skip it once you're collaborating with anyone else on scenes.

## Ongoing hygiene

- Never hand-edit a `.unity`, `.prefab`, or `.asset` (ScriptableObject) file outside the editor
  — see `docs/DECISIONS.md` D12.
- `.meta` files are paired 1:1 with their asset. Renaming or moving an asset must be done
  *inside* the editor (or with `git mv` immediately followed by moving the `.meta` alongside
  it) — never move one without the other.
- If `git status` ever shows an untracked `.csproj` or `.sln`/`.slnx` file you expected to be
  tracked: the standard Unity `.gitignore` blanket-ignores those extensions. Check that the
  negation rules in `.gitignore` (`!/src/**/*.csproj`, `!/tests/**/*.csproj`, `!/*.slnx`) still
  cover the path — see the comment block in `.gitignore` itself.
- Windows-authored files: `.gitattributes` at the repo root normalizes line endings so Unity's
  YAML doesn't churn on every editor save. Don't override it locally.

## Later: CI Android builds

Not wired up yet (that lands around M9–M10 per `docs/FOUNDATION_PLAN.md`). When it is, it will
use `game-ci/unity-builder@v5` reading `unityVersion: auto` from `ProjectSettings/ProjectVersion.txt`
— which is exactly why getting that file right at M3 matters beyond just your local machine.

## Placeholder implementations that MUST be replaced before release

Section 70 says that when a service isn't available, build the abstraction and a temporary
stand-in rather than inventing something that breaks later. Several of those stand-ins now
exist, and each is a release blocker. They live in `Core` (so they compile into a build), which
is convenient during development and dangerous at ship time.

| Placeholder | Replace with | Why it blocks release |
|---|---|---|
| `MockIapProvider` | Unity IAP 5.4.2+ (Google Play Billing 9) | Section 56 forbids fake purchase paths. Shipping this would grant paid content for free. |
| `MockAdProvider` | Google Mobile Ads Unity plugin, **test unit IDs during development** | No revenue, and section 23 forbids production ad IDs before release. |
| `MockAnalyticsProvider` | The chosen analytics SDK (vendor still undecided) | Events go to memory and vanish; every metric in section 77 would read zero. |
| `RunAudio` synthesised blips | Real audio assets with a licence recorded in `LICENSES/THIRD_PARTY_ASSETS.md` | Placeholder sound, and section 57 requires licence provenance for anything shipped. |
| `RunHud` (IMGUI) | uGUI screens built to the design canvas | IMGUI allocates every frame, which section 35 forbids. |
| Capsule/cube primitives | Rigged dinosaur and biome art | Section 83's release checklist requires no placeholder assets. |

The consent flow (UMP) noted in `docs/DECISIONS.md` D8 is also still outstanding and gates
serving ads to EU users.
