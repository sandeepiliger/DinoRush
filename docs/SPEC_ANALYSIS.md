# Spec Analysis — Dino Rush: Extinction Run

This is the analysis pass over `CLAUDE.md` (85 sections) and the UI design canvas
(`docs/design/Dino_Rush_Game_UI_v2.dc.html`, 20 artboards), done before any code was written.
Nothing here is a decision by itself — the calls made against these findings are recorded in
`docs/DECISIONS.md`. This file is the reasoning; that file is the ruling.

## What was reviewed

- `CLAUDE.md` — the 85-section product/architecture spec, committed verbatim at the repo root.
- `docs/design/Dino_Rush_Game_UI_v2.dc.html` — a Claude Design canvas, 20 artboards at
  390×844 (iPhone-shaped, 19.5:9): Splash/Bootstrap, Main Menu, Tutorial, Ready/Countdown,
  Game HUD (Jungle), Biome Unlocked (Desert), Extinction Mode HUD, Pause, Revive Offer, Game
  Over/Results, Collection, Dino Detail/Unlock, Missions, Daily Reward, Daily Challenge + Best
  Runs, Shop, Settings, Service & Error States, First-Run Reward, and a dev-only Debug Panel.
- The repo itself and this container's toolchain (see "Environment reality" below).

## The canvas is a stricter contract than the spec

It's a superset of §39's screen list and consistent with §30's state list, but adds detail the
spec never commits to:

- **Revive**: 4-second countdown, "respawn at 412 m and keep all 37 coins," **one revive per
  run**, ad-ready label with duration shown before the player commits to watching.
- **Save**: Settings reads `SAVE v1`; the error screen restores "last good save (28 Aug,
  21:14)." That implies rolling backup slots + timestamp + atomic write + checksum — more
  concrete than §29's prose.
- **Failure states are fully designed**: ad failed → toast, offline → banner, purchase failed →
  "you have not been charged," save recovered → dialog. This makes §55 checkable rather than
  aspirational — each state is a UI screen to build against, not just a rule to remember.
- **Debug panel** matches §49 and adds a perf HUD (FPS / draw calls / memory). Its numbers
  (`118 draw calls · 214 MB`) are UI *targets* for that screen, not measured facts about the
  actual build — nothing has been profiled yet.

## Contradictions and gaps found

1. **Gems have no sink.** The Main Menu shows a gem counter (36), the Daily Challenge pays 25
   gems, the debug panel grants +100 gems — but §17 only mentions "Gems/rare currency" as a
   day-5 daily reward, and nothing anywhere says what gems buy. See D1.
2. **Dino perks vs. no-pay-to-win.** Every dino in the canvas carries a gameplay modifier
   ("Sprint recharge +10%", "Breaks small obstacles", "shrugs off one hit per run") plus
   Speed/Armour/Jump stat bars, and T-Rex is sold as a ₹399 Legendary. §17 forbids pay-to-win;
   §18 simultaneously forbids rarity being purely cosmetic. Both can't hold once dinos are for
   sale unless perks are constrained. See D2.
3. **Consumable revives appear from nowhere.** The Shop's Starter Pack includes "3 revives,"
   but no spec section defines a revive-as-item, and it collides with the Revive screen's "one
   revive per run." See D3.
4. **Escalation driver is ambiguous.** §5 escalates by time; §16 lists distance, time, score,
   biome, and progression as difficulty inputs; the HUD shows both a metres readout and a
   `DESERT 18S` countdown. A generator can only be validated against one authoritative driver.
   See D4.
5. **Sprint input conflict.** §3 says "hold = sprint"; the HUD draws a dedicated DASH button.
   See D5.
6. **₹ pricing is hardcoded in the design.** Real Play Billing prices are localized and can
   change; the UI must never bake a price string. See D7.
7. **No consent flow anywhere.** §43 covers permissions and secrets but never mentions a
   UMP/consent SDK, which is a Play policy requirement for EU traffic ahead of shipping ads.
   See D8.
8. **Unpicked vendors.** Analytics, crash reporting ("if selected," §66), and remote config all
   have abstractions specified (§28, §52, §70's `IAdProvider`/`MockAdProvider` pattern) but no
   implementation chosen yet. Not a blocker — the placeholder-provider pattern in §70 covers it
   until a vendor is picked.
9. **Aspect ratio.** The canvas is 19.5:9; Android ships from 16:9 to 21:9+. Needs safe-area
   handling and, eventually, a tablet pass — the canvas's own closing note offers exactly that
   retiming.
10. **Identity mismatch.** The repo was named `duckhunt` against a product named *Dino Rush:
    Extinction Run* by Iliger Games. See D10.

## Environment reality (verified this session)

This container has **no Unity, no Blender, no Android SDK/`adb`, no dotnet/mono pre-installed**.
It does have `dotnet-sdk-8.0` installable via `apt` (confirmed with a clean dry-run), reachable
`nuget.org`, ~30 GB free disk, and 4 CPUs.

This is why the architecture puts every rule-checkable, testable piece of gameplay logic (§47,
§48: difficulty scaling, mission progression, save/load, procedural generation validation) into
a Unity-free C# assembly that `dotnet test` can verify right here — see `docs/DECISIONS.md`
D9 and `docs/FOUNDATION_PLAN.md` for the mechanism. Anything requiring the Unity editor (scene
work, rendering, the actual Android build) is handed to milestones that name what a human must
verify on their own machine, per §69.16–17: this project does not claim a result it hasn't
validated.

## Scope risk

§75's MVP (3 biomes, 50 missions, 10+ obstacle types, full monetization) is several times larger
than the §61–63 milestone ladder implies, and the dinosaur art pipeline (§10–13: rig + 9
animations + 3 LODs per dino, times 6 dinos) is the dominant cost with zero 3D tooling present
in this environment. §84 explicitly says "prefer one excellent biome over ten empty ones." The
MVP trim recorded as D6 is the resolution.

## Where this leads

The full milestone ladder, decisions, and setup steps live in `docs/FOUNDATION_PLAN.md` and
`docs/DECISIONS.md`. This file stays as the record of *why* — re-read it before revisiting any
decision, so a future session doesn't re-litigate a contradiction that was already resolved.
