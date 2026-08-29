# Setup

This repo is developed in two places: a Linux container with no Unity installed (used for the
Unity-free `Core` logic and its tests), and your own Windows machine running the Unity editor
(used for everything that needs rendering, scenes, or a device build). This doc covers both.

## Right now: what exists and what doesn't

As of M0–M2, this repo contains **no `ProjectSettings/` directory** — it is not yet a project
Unity can open. That's deliberate (see `docs/DECISIONS.md` D9, D12): the testable game logic is
built and proven first, entirely outside the editor. Unity enters the picture at M3.

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
3. From that throwaway project, copy its `ProjectSettings/` folder into the root of this repo
   (replacing nothing, since this repo doesn't have one yet).
4. Delete the throwaway project — it's served its purpose.
5. Open **this repo** as a Unity project (Hub → Add → select the `dinorush` folder). Unity will
   generate `.meta` files for everything under `Assets/` and resolve `Packages/manifest.json`
   into `Packages/packages-lock.json`.
6. Confirm the console is clean (no red errors) — that's the acceptance bar for M3.
7. Commit: the copied `ProjectSettings/`, the newly generated `.meta` files, and
   `Packages/packages-lock.json`. Do this in one commit before making any other changes —
   skipping this step is what causes GUID drift later ("The referenced script on this Behaviour
   is missing!").

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
