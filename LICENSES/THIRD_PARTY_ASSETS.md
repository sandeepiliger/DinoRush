# Third-Party Assets

Per `CLAUDE.md` §57: the game must have an original visual identity, and no asset — model,
texture, font, sound, or music — may be copied from Chrome Dino, Jurassic Park, Disney, or any
other copyrighted/trademarked source. Dinosaur species names may be used as scientific/common
names; individual character designs must be original. Third-party assets are only usable when
their license explicitly permits commercial use.

This file tracks every third-party asset, font, plugin, and SDK used in the shipped product, so
license compliance can be audited in one place before release (see `CLAUDE.md` §83 final
release checklist).

## How to use this file

Add one row per asset the moment it's brought into the project — not retroactively before
release. Each row needs: what it is, where it's from, its license, and whether that license
permits commercial mobile use.

## Fonts

| Asset | Source | License | Commercial use OK? | Notes |
|---|---|---|---|---|
| Bebas Neue | Google Fonts | OFL 1.1 | Yes | Used in the UI design mock only so far (`docs/design/`); not yet integrated into the Unity project. |
| Barlow / Barlow Condensed | Google Fonts | OFL 1.1 | Yes | Same as above. |
| Archivo Black | Google Fonts | OFL 1.1 | Yes | Same as above. |
| JetBrains Mono | Google Fonts | OFL 1.1 | Yes | Same as above. |

## SDKs / Plugins (planned, not yet integrated — tracked ahead of use per CLAUDE.md §69.13)

| SDK | Publisher | License | Notes |
|---|---|---|---|
| Google Mobile Ads Unity Plugin | Google | Google APIs Terms of Service | Planned for M9 (Ads). Not yet added to `Packages/manifest.json`. |
| Unity IAP (`com.unity.purchasing`) | Unity Technologies | Unity Package/Companion license | Planned for M9 (IAP). |
| Google Play Billing Library | Google | Google Play Developer Terms | Pulled in transitively via Unity IAP. |

## 3D models, textures, audio

None yet — the project currently ships no art assets. Per `CLAUDE.md` §72 (placeholder policy),
early milestones use primitive Unity shapes (capsules, cubes) with no external asset
dependency, so this table starts empty by design rather than by oversight. Update it the moment
any dinosaur model, texture, sound effect, or music track is added, whether hand-made,
commissioned, AI-assisted (§10), or sourced from an asset store.

## Audit checklist before any release build

- [ ] Every row above has a license that explicitly permits commercial mobile distribution.
- [ ] No asset's source attribution requirement is unmet (some OFL/CC fonts require credit
      somewhere in the app — check each font's specific license text, not just "OFL" as a
      category).
- [ ] No placeholder/programmer-art asset with an unclear origin remains in a release build.
- [ ] SDK versions listed here match what's actually pinned in `Packages/manifest.json`.
