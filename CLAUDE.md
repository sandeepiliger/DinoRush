# CLAUDE.md — Dino Park Tycoon

## 1. Project identity

- **Game:** Dino Park Tycoon (working title)
- **Developer:** Iliger Games
- **Genre:** Idle / management tycoon (hybrid-casual)
- **Platform:** Android first (Google Play), built as a web game and packaged for Android
- **Tech:** TypeScript + Three.js + Vite. Unit tests with Vitest. Android packaging via Capacitor (later milestone).
- **Core fantasy:** *Build the world's greatest dinosaur park — city by city.* Hatch dinosaurs, build
  enclosures, attract visitors, upgrade, and expand from Mumbai to Dubai, Tokyo, Paris and New York.

The player experience is the product. Prefer a small number of excellent systems over many mediocre ones.

## 2. Core loop

```
Visitors arrive → pay entry fee → watch dinosaurs → pay tickets → coins
coins → upgrade enclosures / hatch more dinos / upgrade entrance → more income
save up → unlock the next species → finish the park → next city on the world map
offline → park keeps earning (capped) → "welcome back" reward
```

A new player must understand the game within 10 seconds and make a first purchase within ~10 seconds
(this is enforced by `tests/balance.test.ts`).

## 3. Architecture

```
src/
  data/        Content + balancing: species.ts (every dino's look/behaviour), parks.ts (enclosures, economy)
  economy/     Pure economy rules (no DOM/Three.js) — fully unit tested
  sim/         Visitor simulation (pure logic, seeded RNG) — the renderer only reads positions
  core/        Save system (versioned, validated), number formatting
  characters/  Procedural dinosaur builder, animator, instanced visitor crowd, UI portraits
  world/       Park scenery, props, camera, effects (coins, confetti, eggs)
  audio/       Web Audio synthesiser: all SFX + background music generated at runtime
  ui/          DOM UI: styles, SVG icons, localisable strings, helpers
  services/    Ads, analytics, haptics — abstractions with swappable providers
  debug/       Dev-only character gallery (gallery.html, not part of the production build)
  game.ts      Orchestrator — the only file that knows about every subsystem
tests/         Vitest unit tests + balance simulation
scripts/       Headless Chromium screenshot / smoke-test scripts
```

Rules:
- **All balancing numbers live in `src/data/parks.ts`.** Never hardcode costs, rates or rewards in gameplay code.
- **Adding a dinosaur = adding a `SpeciesDef` + an enclosure entry.** The builder, animator, UI and economy
  must not need changes.
- `economy/`, `sim/`, `core/` stay free of DOM and Three.js imports so they remain testable in Node.
- Gameplay code never calls an ad/analytics SDK directly: use `AdManager` and `analytics.track()`.
- No per-frame allocations in hot paths (sim, animator, crowd, effects). Pool and reuse.

## 4. Zero-asset art pipeline

Everything is generated in code — no downloaded models, textures or audio:
- **Dinosaurs:** skinned "loft" bodies swept along a spine curve and bound to a bone chain (tail sway,
  neck bend), sculpted skull + hinged jaw, glossy eyes with catch-lights, muscular legs with toes/claws,
  species features (frill, horns, plates, spikes). Rim-lit standard materials. Static sub-meshes are merged
  per joint to cut draw calls (`mergeStatic.ts`).
- **Animation:** procedural. Gait phase advances with distance walked, so feet don't slide. Tail wave, breathing,
  head look-around, roar (jaw + head raise + camera shake for big species), elastic hatch pop.
- **Visitors:** instanced body parts (≈6 draw calls for the whole crowd), randomised outfits.
- **Audio:** Web Audio synthesis — pentatonic coin chimes, upgrade arpeggios, layered roars, generated music.
- **UI icons:** inline SVG. **Font:** Fredoka (OFL, bundled via @fontsource — no network).

Visual checks: `npm run gallery` (all species side by side) and `npm run screenshot [basic|rich|zoom]`.
Always look at the screenshots after changing characters or scenery. Never claim a visual change works
without viewing it.

## 5. Monetisation (later milestones — design rules apply now)

- Rewarded ads: 2× income boost, 2× offline earnings, (later) free upgrade / instant hatch. Always optional,
  reward stated before watching.
- Interstitials: rare, never on launch, never during an interaction, never right after a rewarded ad,
  frequency-capped, disabled by Remove Ads.
- IAP: Remove Ads, permanent 2× income, starter pack, premium dinos. Google Play Billing only.
- **Development uses test ads only** (`MockAdProvider` today). Never commit ad unit IDs, keys or secrets.
- Never pay-to-win pressure, never manipulative timers. The full game must be enjoyable without paying.

## 6. Save system

`SaveManager` stores versioned JSON (`saveVersion`), keeps a backup copy, validates and clamps every field
on load, rejects saves from newer versions, and falls back to backup → fresh save on corruption. A
`lastSeen` in the future earns nothing offline (basic clock-tamper protection). Add migrations step by step
when `SAVE_VERSION` increases.

## 7. Performance budget (mid-range Android)

- 60 FPS target, 30 FPS floor. The renderer drops pixel ratio automatically if frames are slow.
- Current scene: ~165 draw calls, ~130k triangles with a full park. Keep draw calls under ~250.
- Production bundle: ~180 KB gzipped (JS + CSS + fonts). Keep it small; no large dependencies.
- Profile on a real device before optimising blindly. Do not claim performance numbers without measuring.

## 8. Milestones

1. ✅ **Playable park (this milestone):** one city (Mumbai), 5 species, visitors, economy, upgrades,
   hatching, entrance, offline earnings, 2× boost (mock ad), save/load, audio, tutorial hint, settings.
2. **Juice & retention:** daily reward, missions/achievements, park rating stars, visitor thought bubbles,
   dino feeding interaction, better unlock cinematic, haptics tuning.
3. **World map:** city map screen, second city (Dubai) with its own theme and species, prestige/city transfer.
4. **Monetisation:** AdMob via Capacitor plugin (test IDs), Remove Ads + 2× income IAP via Play Billing,
   analytics provider (e.g. Firebase), remote config for balancing.
5. **Android release:** Capacitor project, icons/splash, AAB, signing config (never committed), Play Console
   internal → closed testing (12 testers × 14 days for new personal accounts), store listing.

## 9. Validation before calling anything done

Run all of these; do not claim success if any fails:
```bash
npm run typecheck
npm test               # economy, balance pacing, save/load, visitor simulation
npm run build
npm run check:prod     # loads the production bundle headless; fails on any page error
npm run screenshot     # then LOOK at screenshots/*.png
```

## 10. Operating rules for Claude Code

1. Read this file, inspect the code, then make the smallest complete change.
2. Keep the project buildable and tests green after every change.
3. Never fabricate test results or claim something works without running/viewing it.
4. Reuse existing systems; don't create duplicates. Keep modules independent.
5. Never commit secrets, keystores, ad IDs or production credentials.
6. Only use third-party code/assets whose license allows commercial use; record them in
   `LICENSES/THIRD_PARTY_ASSETS.md`.
7. Don't copy other games' art, characters, logos or names (e.g. no Jurassic Park look-alikes).
8. Document non-obvious decisions in code comments or here.
