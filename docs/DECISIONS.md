# Architecture Decision Records — Dino Rush: Extinction Run

Each entry: the decision, what it resolves, and the trigger that would justify revisiting it.
Background reasoning lives in `docs/SPEC_ANALYSIS.md`; the milestone plan that builds on these
is `docs/FOUNDATION_PLAN.md`. All decisions here were confirmed with the project owner.

---

### D1 — Gems cut from MVP

**Decision:** Coins are the only currency shipped in MVP. The economy code models currency as
a `CurrencyType` enum (or equivalent extensible type) so a second currency can be added later
without a rewrite.

**Resolves:** The design canvas shows a gem counter, a gem reward on the Daily Challenge
screen, and a debug-panel gem grant — but no spec section defines what gems are spent on. A
currency with no sink is dead UI.

**Revisit when:** A concrete gem sink is designed (e.g. cosmetic-only premium currency, or a
cross-progression system for future Iliger Games titles per §79).

---

### D2 — Dino perks are sidegrades, never power

**Decision:** Every dinosaur's gameplay perk (e.g. "Sprint recharge +10%," "shrugs off one hit
per run") must have an earnable equivalent reachable through play, not purchase alone. No perk
may be a strict numerical upgrade unavailable without paying.

**Resolves:** §17 forbids pay-to-win; §18 forbids purely-cosmetic rarity. The canvas sells a
Legendary T-Rex for ₹399 with stat bars (Speed/Armour/Jump) — a plain reading of both sections
together would make that pay-to-win the moment it ships. Sidegrades let rarity carry real
gameplay identity (§18) without buying power (§17).

**Revisit when:** Post-launch metrics (§77–78) show monetization is the bottleneck and design
wants to explore power-adjacent purchases — that's a live-ops call, not a code constraint, and
should be re-opened deliberately, not by drift.

---

### D3 — Consumable revives cut from MVP

**Decision:** Exactly one rewarded-ad revive per run, as designed on the Revive Offer screen.
No purchasable revive items in MVP.

**Resolves:** The Shop's Starter Pack lists "3 revives," which is an item class defined nowhere
in the spec and directly collides with the Revive screen's own "one revive per run" copy.

**Revisit when:** A revive-economy design exists that reconciles the two (e.g. purchased
revives only unlock in Extinction Mode, or only after the rewarded revive is spent).

---

### D4 — Time is the authoritative escalation driver

**Decision:** The extinction escalation timeline (§5) is driven by elapsed run time, fully
deterministic under a seed. Distance and score are computed *from* the run and displayed, but
never feed the difficulty curve.

**Resolves:** §5 escalates by time; §16 lists five drivers (distance, time, score, biome,
progression); the HUD shows both metres and a biome countdown in seconds. A generator can only
be exhaustively tested (§48) against one authoritative input — testing five interacting drivers
combinatorially isn't tractable for "generate thousands of segments and verify validity."

**Revisit when:** Playtesting shows time-only escalation feels unfair to slower/faster runners
and a hybrid (e.g. time gated by a minimum distance) is designed and specified precisely enough
to test.

---

### D5 — DASH button is primary; hold is an alias

**Decision:** Sprint/dash is triggered by a dedicated on-screen button (as drawn in the Game
HUD artboard). Press-and-hold anywhere is supported as a secondary input path to the same
action, not a separate mechanic.

**Resolves:** §3 specifies "hold = sprint"; the HUD design draws a discrete DASH button. A
button is more discoverable and reads better for one-handed play (§3's own requirement); the
hold-alias preserves the spec's original text at no extra cost.

**Revisit when:** Never expected to need revisiting — this is additive, not exclusive.

---

### D6 — MVP scope trim (confirmed by project owner)

**Decision:** MVP ships **one dinosaur (Velociraptor)**, **two biomes** — Prehistoric Jungle
escalating directly into the Volcanic Land extinction climax — and **approximately 12
missions**, rather than §75's 3 biomes / 6 dinosaurs / 50 missions. Desert, the remaining five
dinosaurs, and the rest of the mission catalog become post-MVP content updates, added through
the same data-driven systems (§50) so no core code changes when they land.

**Resolves:** §84 explicitly states "prefer one excellent biome over ten empty ones" and "one
excellent dinosaur over twenty unfinished dinosaurs." The dinosaur asset pipeline (§10–13: rig
+ 9 animations + 3 LODs, per dino) is the single largest production cost in the entire project,
and this environment currently has **zero** 3D/Blender tooling available to accelerate it.
Building six full dinosaurs before validating the core loop is fun would invert §72's own
placeholder policy ("prove the gameplay before spending art hours").

**Revisit when:** The Jungle→Volcanic vertical slice is playtested and validated as fun, at
which point Desert and additional dinosaurs are the natural next content milestones (already
slotted after M6 in `docs/FOUNDATION_PLAN.md`).

---

### D7 — Prices are never hardcoded

**Decision:** No UI element renders a literal price string. All prices come from Google Play
Billing's `ProductDetails` at runtime, localized to the player's store region and currency.

**Resolves:** The Shop artboard bakes ₹149 / ₹249 / ₹399 directly into the design. Those are
placeholder values for the mock only — a real build must never hardcode a currency figure,
both because Play requires it and because prices change without a code release.

**Revisit when:** Never — this is a correctness rule, not a scope call.

---

### D8 — UMP consent SDK is a ship-blocker for ads

**Decision:** Google's User Messaging Platform (or equivalent consent SDK) is treated as a
required dependency of the Ads milestone (M9), not an optional add-on, and must gate ad
initialization for applicable regions before any ad request is made.

**Resolves:** §43 covers permissions and secrets but never mentions consent management, which
is a Google Play policy requirement for serving ads to EU/UK/similar-regulated traffic. The
Settings screen already reserves an "Ad preferences" row this can hang off.

**Revisit when:** Not expected to be revisited — this tracks external platform policy, not
internal design preference.

---

### D9 — Core gameplay logic is Unity-free, and the boundary is enforced twice

**Decision:** All rule-checkable gameplay logic — difficulty curve, procedural segment
generator and its validator, economy, score, mission evaluation, daily reward/streak logic,
save schema and migration — lives in `Assets/Scripts/Core/`, an assembly with **zero
`UnityEngine` references**. The boundary is enforced two independent ways:

1. In Unity, `DinoRush.Core.asmdef` sets `"noEngineReferences": true`, so a stray
   `using UnityEngine;` inside Core is a compile error in the editor.
2. Outside Unity, `src/DinoRush.Core.csproj` is a plain `netstandard2.1` project that globs the
   *same* `.cs` files under `Assets/Scripts/Core/` — there are no `UnityEngine` assemblies on
   its reference path at all, so the same violation fails `dotnet build` even without Unity
   installed.

`tests/DinoRush.Core.Tests/` then runs ordinary `dotnet test` against that project, including
the §48 procedural-generation safety suite ("generate thousands of segments and verify
validity"), entirely from a plain Linux container with no Unity, Blender, or Android SDK
present.

**Resolves:** This container has no Unity, so any architecture that required the editor to
validate gameplay logic would leave every early milestone unverifiable, directly conflicting
with §69.16–17 ("never fabricate successful test results," "never say something works without
actually validating it"). Making the boundary double-enforced means it can't quietly rot even
after Unity is introduced in M3 — a future contributor adding a `UnityEngine.Time.deltaTime`
call inside Core breaks the build immediately, in the editor and in CI.

**Mechanical constraints this implies** (see `docs/FOUNDATION_PLAN.md` for the full layout):
Core targets `netstandard2.1`, `LangVersion 9.0`, `ImplicitUsings disable`, `Nullable disable`,
`GenerateAssemblyInfo false` to match what Unity 6 actually compiles against; MSBuild output
paths are redirected outside `Assets/` so stray `obj/`/`bin/` folders don't get imported as
stray assemblies by Unity; Core carries its own lightweight value types (not
`UnityEngine.Vector3`) with conversion extensions living in the Runtime assembly instead.

**Revisit when:** Not expected to be revisited — this is the load-bearing architectural
decision of the whole project and the one piece proven executable in this environment today.

---

### D10 — Product identity

**Decision:** Application ID `com.iligergames.dinorush`; product name *Dino Rush: Extinction
Run*; publisher Iliger Games; GitHub repository `sandeepiliger/DinoRush`.

**Resolves:** The repository was originally created and named `duckhunt`, mismatched against
every other reference to the product across the spec and design canvas. Renamed by the project
owner this session.

**Revisit when:** Never expected to change absent a rebrand.

---

### D11 — Unity version pin: 6000.3.23f1 (Unity 6.3 LTS)

**Decision:** `ProjectSettings/ProjectVersion.txt` pins editor version `6000.3.23f1`.

**Resolves:** Verified 2026-08-29: Unity 6.0 LTS support ends ~Oct 2026 (about five weeks from
this decision); Unity 6.5 is a rolling "Supported release" that stops receiving fixes the
moment 6.6 ships, so it isn't safe to lock a production project to; 6.6/6.7 are pre-release;
Unity 2022.3 LTS is already end-of-life for the Personal/Pro tiers this project uses. Unity 6.3
LTS shipped Dec 2025 and is supported through **Dec 2027**, the longest runway of any currently
shipping build, with the widest current Asset Store/GameCI Docker coverage. Also confirmed at
the same time: Unity's Runtime Fee was cancelled in 2024 and has not returned; Personal tier is
free up to $200k revenue; the "Made with Unity" splash is optional on Unity 6 — none of these
block a small-team Android release on the free tier.

**Revisit when:** Unity 6.7 LTS ships (expected late 2026) and has matured — re-evaluate only
if 6.3's support window becomes a concern relative to this project's expected ship date, not
proactively.

---

### D12 — No hand-authored Unity YAML, ever

**Decision:** No `.unity` scene file, `.prefab`, or ScriptableObject `.asset` file is
hand-written by an agent working outside the Unity editor. Runtime object graphs for early
milestones are constructed in code (a bootstrap `MonoBehaviour` building what it needs at
startup); content data that would naturally be a ScriptableObject is instead authored as plain
JSON deserialized into C# config objects until the project is opened in the editor, at which
point it can be converted to real ScriptableObject assets by hand.

**Resolves:** Unity's YAML for scenes/prefabs is `fileID` + GUID + `serializedVersion` soup
that differs across editor versions and has no independent validator outside the engine itself.
A malformed hand-written scene fails to import silently or with an unhelpful error — exactly
the "looks fine here, breaks on your machine" failure mode this whole architecture exists to
avoid. `.asmdef` files, by contrast, are documented as safe to hand-edit (plain JSON, and using
assembly *names* rather than GUIDs in `references` avoids `.meta`-file coordination entirely),
so those remain hand-authored.

**Revisit when:** Once the project has been opened at least once on a real editor (post-M3) and
a human can immediately verify a hand-edit by pressing Play, this restriction can loosen for
small, low-risk YAML tweaks — but the default stays code-first.
