# Dino Park Tycoon

An idle park-management game by **Iliger Games**. Hatch dinosaurs, build enclosures, attract visitors
and grow your park — city by city.

Every character, prop, icon, sound effect and music track is **generated in code**: no downloaded art or
audio assets.

<p>
  <img src="docs/screenshots/park.png" width="240" alt="Full park">
  <img src="docs/screenshots/upgrade.png" width="240" alt="Upgrade panel">
  <img src="docs/screenshots/unlock.png" width="240" alt="Unlocking a new dinosaur">
</p>

*Screenshots are real captures of the current build (headless Chromium, 412×870 phone viewport).*

## Run it

```bash
npm install
npm run dev        # open the printed URL on your phone (same Wi-Fi) or in a desktop browser
```

## Check it

```bash
npm run typecheck
npm test           # economy, balance pacing, save/load, visitor simulation
npm run balance    # prints how long a smart player takes to unlock each species
npm run build      # production bundle in dist/
npm run check:prod # loads the production bundle in headless Chromium, fails on any error
npm run screenshot # scripted play session -> screenshots/*.png  (basic | rich | zoom)
npm run gallery    # every dinosaur side by side -> screenshots/
```

The character gallery is also available in dev at `/gallery.html?view=side&anim=walk` (views:
`side|front|top|three`, animations: `idle|walk|roar`, optional `&species=trex`).

## Project docs

- **[CLAUDE.md](CLAUDE.md)**: architecture, rules, art pipeline, monetisation principles, milestones.
- **[LICENSES/THIRD_PARTY_ASSETS.md](LICENSES/THIRD_PARTY_ASSETS.md)**: third-party code and fonts.

## License

Proprietary — © Iliger Games.
