# Dino Rush: Extinction Run — Foundation Plan

## Context

`sandeepiliger/DinoRush` (renamed from `duckhunt`) contains exactly one file: a 16-byte
`README.md`. No Unity project, no code, no docs. Everything we have is a specification
(`CLAUDE.md`, 85 sections) and a 20-artboard UI design canvas — both currently only chat
attachments that vanish when this session ends.

Two problems drive this plan:

1. **Nothing is durable.** The spec analysis lives only in this conversation. The branch
   `claude/dino-rush-spec-analysis-5lxtvb` was never actually pushed — `git ls-remote` shows the
   remote has only `main` — so there is nothing to carry forward but transcript text.
2. **This container cannot build Unity.** No Unity, no Blender, no Android SDK, no `adb`.
   Per §69.17 and §80 we may not claim a feature works without validating it, so any approach
   requiring the editor to verify *anything* leaves every milestone unclaimable.

Intended outcome: a repo where near-term work is **fully verifiable in this container**, the
Unity-side work is **incremental and independently openable on your Windows machine**, and every
milestone commits on its own without leaving the tree broken.

---

## The finding that shapes everything

**`dotnet-sdk-8.0` is installable here** — apt candidate `8.0.125`, dry-run clean, nuget.org
reachable, 30 GB free, 4 CPUs. Verified this session.

So the central architectural decision from the analysis is not merely sound, it is executable today:

> **All gameplay logic lives in a pure C# assembly with zero `UnityEngine` references, compiled
> and unit-tested via `dotnet test` — no editor required.**

Difficulty curve, procedural segment generator **and its validator**, economy, score, missions,
daily reward/streak, save schema and migration, unlock rules. Only MonoBehaviour glue, scenes and
rendering need Unity.

This buys a property worth stating plainly: **the boundary is mechanically enforced, twice.**
Unity's side uses `"noEngineReferences": true` in the Core `.asmdef` (a compile error, not a
convention); CI's side is `dotnet build`, which has no Unity assemblies to resolve at all. The
rule cannot quietly rot.

### One source, two compilers

```
Assets/Scripts/Core/          ← pure C#, the single source of truth
  DinoRush.Core.asmdef        { "noEngineReferences": true }
Assets/Scripts/Runtime/       ← MonoBehaviours, adapters (references Core)
src/DinoRush.Core.csproj      ← netstandard2.1, globs ../Assets/Scripts/Core/**/*.cs
tests/DinoRush.Core.Tests/    ← net8.0, NUnit, ProjectReferences the above
```

No duplication, no symlinks, and **no file moves later** — Core sits in its final home before
Unity ever opens the project.

Research surfaced five settings that must match Unity or code will build here and fail there:
`netstandard2.1` on Core, `LangVersion 9.0`, `ImplicitUsings disable`, `Nullable disable`,
`GenerateAssemblyInfo false`. Also `BaseOutputPath`/`BaseIntermediateOutputPath` are redirected
outside `Assets/` — MSBuild intermediates landing under `Assets/` make Unity import stray DLLs and
throw duplicate-definition errors.

And a genuine trap: **the canonical Unity `.gitignore` ignores `*.csproj` and `*.sln`.** Without
explicit negations for `src/**` and `tests/**`, our entire test harness would be silently
untracked. M0 handles this.

Core also cannot use `UnityEngine.Vector3`, so it carries its own small value types with
conversion extensions living in the Runtime assembly.

---

## Decisions carried forward

Recorded as ADRs in `docs/DECISIONS.md`. All reversible; each records its trigger.

| # | Decision | Rationale |
|---|---|---|
| D1 | **Gems cut from MVP.** Coins only; `CurrencyType` stays extensible. | The design shows a gem counter, gem rewards and a debug grant, but nothing anywhere says what gems *buy*. A currency with no sink is dead UI. |
| D2 | **Dino perks are sidegrades, never power.** Every purchasable dino's perk has an earnable equivalent. | Resolves §17 "no pay-to-win" vs §18 "rarity not purely cosmetic", which a ₹399 premium T-Rex with stat bars would otherwise violate. |
| D3 | **Consumable revives cut.** One rewarded revive per run, as the Revive screen states. | Starter Pack's "3 revives" invents an item class in no spec section and collides with "one revive per run". |
| D4 | **Time is the authoritative escalation driver;** distance is display-only. Fully seeded. | §5 escalates by time, §16 lists five drivers, the HUD shows both. One driver, or the curve is untestable. |
| D5 | **DASH button primary,** hold-anywhere as alias. | §3 says hold, the HUD draws a button. Button reads better one-handed; the alias is free. |
| D6 | **MVP: 1 dino (Velociraptor), Jungle → Volcanic extinction climax, ~12 missions.** Desert, 5 dinos, remaining missions become content updates. *(confirmed)* | §75 asks 3 biomes/6 dinos/50 missions; §84 says "prefer one excellent biome over ten empty ones". The art pipeline (§10–13: rig + 9 animations + 3 LODs each) is the dominant cost with zero tooling present. |
| D7 | **Prices never hardcoded** — rendered from Play Billing `ProductDetails`. | The canvas bakes ₹149/₹249/₹399; real pricing is localized by Play. |
| D8 | **UMP consent SDK is a ship-blocker for ads.** | §43 never mentions consent; it's a Play policy requirement for EU traffic. Settings already has an "Ad preferences" row to hang it on. |
| D9 | **Core is Unity-free, enforced by `noEngineReferences` + CI.** | See above. |
| D10 | **Identity:** `com.iligergames.dinorush`, *Dino Rush: Extinction Run*, Iliger Games. | Repo/product/appId were all mismatched. |
| D11 | **Pin Unity 6.3 LTS `6000.3.23f1`.** | Verified current: Unity **6.0 LTS support ends Oct 2026** (~5 weeks), 6.5 is a rolling release that stops getting fixes when 6.6 ships, 6.6/6.7 are pre-release. 6.3 LTS runs to **Dec 2027**. 2022.3 is already EOL for Personal. Also: Runtime Fee is cancelled, Personal is free to $200k, splash screen optional on Unity 6. |
| D12 | **No hand-authored scene/prefab/`.asset` YAML. Ever.** Code-driven bootstrap instead. | Unity YAML is `fileID`+GUID+`serializedVersion` soup; a malformed scene fails to import with no useful error — precisely the "breaks on your machine" outcome we're avoiding. |

---

## Milestone ladder

Each milestone is independently committable and leaves the tree working. The verification column
is the honest one: what can actually be proven, and by whom.

### Provable entirely in this container

| M | Deliverable | Verified by |
|---|---|---|
| **M0** | **Repo foundation.** Branch recreated from `main`. `CLAUDE.md` committed to root so every future session reads the spec. `docs/SPEC_ANALYSIS.md`, `docs/DECISIONS.md`, `docs/SETUP.md`. Unity `.gitignore` **+ negations for `src/**/*.csproj`, `tests/**/*.csproj`, `*.slnx`**. `.gitattributes` (LF normalization + Unity YAML merge driver — you're on Windows, so this prevents CRLF churn in every diff). `LICENSES/THIRD_PARTY_ASSETS.md` (§57). README. Design canvas archived to `docs/design/`. | Docs and config only — nothing to break. |
| **M1** | **Core skeleton + CI.** `Assets/Scripts/Core/` with `DinoRush.Core.asmdef`, `src/DinoRush.Core.csproj`, `tests/DinoRush.Core.Tests/` (NUnit). GitHub Actions running `dotnet test` on push. `.claude/settings.json` SessionStart hook installing the SDK so future web sessions are productive immediately. | **`dotnet test` green here.** |
| **M2** | **Core game logic.** Seeded deterministic RNG, `DifficultyConfig` + curve, `SegmentGenerator` + `SegmentValidator`, economy/score, mission evaluation, daily reward + streak, save schema v1 + migration harness. | **`dotnet test` green here**, including §48's "generate thousands of segments and verify validity" — no impossible jumps, no unavoidable obstacles, minimum reaction time enforced. |

M0–M2 ship **no `ProjectSettings/`**, so the tree is inert to Unity. Loose `.cs` under `Assets/`
is harmless until a project exists around it, and we ship **no `.meta` files and no YAML
referencing script GUIDs** — the one combination research flags as always-safe.

### M3 — Unity baseline handoff *(the one step that starts on your machine)*

Research changed my approach here. `ProjectSettings/ProjectSettings.asset` — which holds bundle
ID, IL2CPP, ARM64, min/target SDK — is a huge `serializedVersion`-stamped YAML that differs
between editor versions. Synthesizing it blind is the riskiest thing we could do, so we don't.

**You:** create a throwaway Unity **6000.3.23f1** URP Mobile project via Unity Hub (with Android
Build Support), copy its `ProjectSettings/` into the repo, open the repo project once, commit
`ProjectSettings/`, the generated `.meta` files, and `Packages/packages-lock.json`.

**Me:** everything around it — `Packages/manifest.json`, `.asmdef` wiring, §32 folder
architecture, the code-driven bootstrap, editor tooling, and a `docs/SETUP.md` giving you the
exact click-path plus a Player Settings checklist.

*Verified by:* project opens with a clean console.

### M4 — First playable run (§61)

PlayerController (tap / swipe-down / dash), camera, one obstacle, collision, score, death,
restart — capsule placeholders per §72, object graph built in code per D12.
*Verified by:* you press Play. Acceptance = launch → run → jump → die → restart.

### Then, in §74 phase order

M5 procedural + pooling + coins (§62) · M6 biomes + extinction escalation (§63) · M7 UI shell
against the canvas (screens 01–10) · M8 collection/missions/daily (§64, screens 11–15) · M9 ad +
IAP abstractions with mock providers (§65, §70) · M10 analytics, build automation, save
migration, localization (§66) · M11 polish (§67) · M12 release candidate (§68).

Monetization stays last, exactly as §74 orders.

---

## Release-gate facts to bank now (not action yet)

Dated research, relevant at M9/M12 — recorded in `docs/DECISIONS.md` so they aren't rediscovered
late:

- **Google Play Billing Library 8+ is mandatory for new apps and updates as of Aug 31 2026** —
  *two days from today* (extensions to Nov 1 2026). Unity IAP `com.unity.purchasing` **5.4.2**
  ships GPBL 9.0.0 and clears it. Unity IAP 4.x is end-of-support; do not start on it.
- **Target API 36 (Android 16)** required for new apps/updates from Aug 31 2026.
- Google Mobile Ads Unity plugin **v11.4.0** is current.
- CI later: `game-ci/unity-builder@v5` (not the `@v4` every tutorial still shows, nor the `@v6`
  beta rewrite), `androidExportType: androidAppBundle`, `unityVersion: auto` — which reads the
  `ProjectVersion.txt` we pin in M3. Needs a free-disk-space step and a `Library/` cache, plus a
  Unity license secret you'd add.

These bind nothing now — we aren't shipping — but they set the versions M9 targets.

---

## Verification

**Here, every commit from M1 onward:**
```bash
dotnet test tests/DinoRush.Core.Tests   # includes the §48 procedural-safety suite
```
Plus the same job in GitHub Actions on push, so a broken core never reaches `main`.

**On your machine, at M3 and M4:**
1. Unity Hub → open at `6000.3.23f1` → loads with a clean console.
2. Commit the `.meta` files and `packages-lock.json` Unity generates on first open — this is the
   step that, if skipped, causes GUID drift and "referenced script is missing" later.
3. M4: press Play → run, jump, duck, hit an obstacle, die, restart.

**What I will not claim:** that anything compiles in Unity, that the Android build works, or that
performance meets §35/§60. None is provable from here and §69.17 forbids asserting it. The debug
panel's `118 draw calls · 214 MB` stay *targets*, not measurements, until profiled on a device.

---

## Scope of the first execution pass

On approval I will do **M0 → M2**: recreate the branch, land the docs and repo config, stand up
the tested Core, and commit each milestone separately. Then I'll hand you the short M3 checklist.

I will not touch `ProjectSettings/` or author any Unity YAML.
