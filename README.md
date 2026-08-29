# Dino Rush: Extinction Run

An Android-first, hybrid-casual endless runner built in Unity + C# by Iliger Games. The player
controls a dinosaur fleeing forward while the world behind it progressively collapses —
jungle, into desert, into a volcanic extinction-level climax.

Run. Survive. Escape Extinction.

## Start here

- **[CLAUDE.md](CLAUDE.md)** — the full product and architecture specification. Read this
  before touching anything; every design and engineering decision in this repo traces back to
  a numbered section in it.
- **[docs/SPEC_ANALYSIS.md](docs/SPEC_ANALYSIS.md)** — the analysis pass over the spec and UI
  design: contradictions found, gaps identified, environment constraints discovered.
- **[docs/DECISIONS.md](docs/DECISIONS.md)** — the resulting architecture decisions (ADRs),
  each with its rationale and the trigger that would justify revisiting it.
- **[docs/FOUNDATION_PLAN.md](docs/FOUNDATION_PLAN.md)** — the milestone ladder from an empty
  repo to a playable vertical slice, and beyond.
- **[docs/SETUP.md](docs/SETUP.md)** — how to build and test the Core logic (any machine with
  the .NET SDK), and how to bring Unity into the repo for the first time.
- **[docs/design/](docs/design/)** — the UI design canvas (20 screens) this project's front-end
  is being built against.

## Current status

Pre-Unity. The gameplay-critical logic (difficulty curve, procedural segment generation and
validation, economy, missions, save system) is being built and tested as a Unity-free C#
assembly first — see `docs/DECISIONS.md` D9 for why. The Unity project itself lands at
milestone M3.

```bash
dotnet test tests/DinoRush.Core.Tests
```

## License

Proprietary — © Iliger Games. Third-party asset licenses are tracked in
[LICENSES/THIRD_PARTY_ASSETS.md](LICENSES/THIRD_PARTY_ASSETS.md).
