# Setup

This repo is developed in two places: a Linux container with no Unity installed (used for the
Unity-free `Core` logic and its tests), and your own Windows machine running the Unity editor
(used for everything that needs rendering, scenes, or a device build). This doc covers both.

## Right now: what exists and what doesn't

M0–M2 built and proved the testable game logic entirely outside the editor — deliberately, see
`docs/DECISIONS.md` D9 and D12. M3 is in progress: `ProjectSettings/` has been copied in from a
Unity 6000.3.23f1 URP template, and `Assets/Scripts/Runtime/` now holds the first code that
crosses into the engine. Still outstanding before the project opens cleanly: `Packages/` and the
template's `Assets/` contents (step 3 below).

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
