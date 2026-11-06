# H2 Historical World 3D — MVP

A new, **independent** historical world engine for BanDoSuVideo. It is not an update to the legacy 2D or Terrain packs. Its core concept is **one geographic world, multiple historical epochs**, with repeatable building lifetimes and camera paths.

## Capabilities in this PR

- 3D terrain and schematic river, procedural instanced trees, buildings/towers/walls in Three.js.
- Stable building IDs, from/to historical year lifetime and deterministic epoch switching.
- Timeline scenes referencing one epoch and a from/to/target camera path.
- H2-compatible `--check`, `--sheet`, `--thumb`, `--from`, `--to` and full render modes.
- Python project / voice / shots / mix helpers compatible with existing WPF StudioCommands.
- Importable engine ZIP built by `H2 Historical World 3D CI` action; **ZIP excludes node_modules**.
- Three sample epochs and 420 synthetic buildings. **All example history is fictional**; not a reconstruction of Jerusalem or Thăng Long.

## V2 visual quality upgrade (opt-in, deterministic)

V2 improves the existing Three.js procedural renderer without changing the world history format or requiring external paid assets:

- Sculpted surface relief with deterministic ground vertex colors; the river corridor is carved into the ground.
- Water is a triangulated flat ribbon following the river (no longer a round tube).
- Optional `world.roads.paths` roads draped over terrain and deterministic tree clearings by the settlement and river.
- Houses/towers/walls receive separate roofs, soft shadow maps and improved atmospheric lighting.
- Camera motion supports smooth ease-in/out and optional `args.camera.mode: "orbit"` plus `angle`; original `from`, `to`, `target` remain compatible.
- New project samples include roads and closer camera positions; older `history/world.json` files still load.

Render test: `npm test`, then `python tools/new_project.py --name visual-v2 --title "Visual V2" --pack historical` followed by `node render.mjs --project projects/visual-v2 --sheet --scale 0.5`.

**Scope**: This is a stylized procedural 3D visual improvement, not an accurate historically sourced reconstruction, professional architectural assets, real GIS or photorealism. Evaluate resulting thumbnail/sheet visually before production.

## V3 planned historical city and event timeline

V3 adds a synthetic road-aligned town layout with district metadata, civic landmarks, four gateways and a ring of fortifications in the **final epoch only**. The demo is imaginary, not a claim about an actual ancient city.

- `tools/city_generator.py` creates deterministic houses aligned with primary/secondary streets and river-clearance zones.
- Stable object IDs make the same history/world editable across epochs.
- `events` in `history/world.json` support year-indexed `changes: [{id,action}]` with actions `show` and `hide`. Events are replayed chronologically on every frame, so scrubbing backwards is deterministic.
- Additional schematic `market`, `temple`, and `gate` meshes appear as landmarks.
- Original V2 worlds without events remain supported.

Try on Windows from the engine directory:

```cmd
npm install
npx playwright install chromium
npm test
python tools/new_project.py --name city-v3 --title "City V3" --pack historical
node render.mjs --project projects/city-v3 --sheet --scale 0.5
node render.mjs --project projects/city-v3 --from 0 --to 24 --scale 0.5
```

Inspect `projects/city-v3/build/sheets/sheet_01.jpg` and `projects/city-v3/build/preview.mp4`.

**Limitations:** Grid-based zoning is intentionally simplified; fortification geometry is schematic, roads may cross the procedural river without bridges, and no production GIS, authentic architectural assets, damage animation, archaeological verification or AI scene planning is included. For story-specific reconstructions, replace demo geometry with sourced world data.

## V3.5 — City visual quality and road hierarchy

The V3.5 procedural demo addresses overly prominent black street grids and crowded housing in the V3 storyboard:

- Roads are shallow ground-conforming ribbons with earth-toned translucent materials, no cast shadows, and narrower primary/secondary/alley widths.
- Roads are revealed by historical year using optional `{id,pts,kind,from,to}` objects in `world.roads.paths`. Older V2/V3 point-array roads remain supported.
- River banks automatically clip road geometry. Bridges are **not** synthesized, so roads stop at the shore.
- The generated demo has streets of different lengths, fewer oversized cross-map lines, building setbacks, courtyards, landmark clearance, river clearance and a broader house material palette.
- Fortification/gate lifetime and timeline IDs remain unchanged.

**Review scope:** Running `npm test` and `python -m unittest discover -s tools -p test_city_generator.py` verifies road rules, temporal visibility and city layout determinism. Real image acceptance still requires Chromium/FFmpeg and inspection of `sheet_01.jpg` (particularly the early settlement with only its central axes). Do not infer professional architectural fidelity from passing tests.

Create a new sample rather than reusing an old V3 `projects/city-v3` directory:

```cmd
python tools/new_project.py --name city-v35 --title "City V3.5" --pack historical
node render.mjs --project projects/city-v35 --sheet --scale 0.5
node render.mjs --project projects/city-v35 --from 0 --to 24 --scale 0.5
```

Historical authenticity, bridges, curved city walls, imported high-detail architectural assets and physically validated urban layout remain separate follow-up tasks.

## V4 — architecture and urban reconstruction baseline

V4 upgrades the **synthetic demonstration**, without introducing unsupported historical claims:

- Eight procedural residential variants (`gable`, `hip`, `courtyard`, `longhouse`, `workshop`, `storehouse`, `two-storey`, `terraced`). Stable building IDs produce reproducible fallback styles for older projects.
- New `engine/architecture.mjs` produces instanced walls, roofs, doors, extensions and civic architecture. Specialized wall battlements, corner towers, and open gate passages replace plain box-like city fortifications.
- Adjacent districts are staggered slightly rather than following identical rectilinear blocks.
- Sample camera positions remain consistent across epochs to make settlement expansion and final fortification visually comparable.
- The existing historical `show/hide` event system and V3.5 river-aware phased roads remain unchanged.
- Multiple meshes per architectural variant cost more draw calls and memory than V3.5. Check the full 24-second render and memory on the target hardware.

To generate a **fresh** V4 sample (not an existing V3.5 project):

```cmd
npm install
npx playwright install chromium
npm test
python tools/new_project.py --name city-v4 --title "Historical City V4" --pack historical
node render.mjs --project projects/city-v4 --sheet --scale 0.5
node render.mjs --project projects/city-v4 --from 0 --to 24 --scale 0.5
```

Inspect `projects/city-v4/build/sheets/sheet_01.jpg` and `build/preview.mp4`. The target is visibly distinct house silhouettes, clear city walls/gates, and the same comparable framing across all three historical epochs.

**Limits:** Procedural models are still stylized and not region-/culture-specific GLB assets. This increment does not introduce full GIS terrain reconstruction, new bridges, physically accurate water shaders, cinematic destruction sequences, or source-verified historical settlements. More advanced simulation and asset-import authoring should follow after visual QA.

## Development

```bash
cd engines/h2-historical-world
npm install
npx playwright install chromium
node --test tests/*.test.mjs
python tools/new_project.py --name historical-demo --title "Historical World Demo" --pack historical
python tools/doctor.py
node render.mjs --project projects/historical-demo --check --scale 0.5
node render.mjs --project projects/historical-demo --sheet --scale 0.5
node render.mjs --project projects/historical-demo --from 0 --to 3 --scale 0.5
node render.mjs --project projects/historical-demo
python tools/mix.py --project projects/historical-demo
```

Requires Node.js 22+, Python 3.10+, FFmpeg+FFprobe and a working Chromium. No other Python packages are needed.

## Installing into BanDoSuVideo

Download the `H2-Historical-World-3D-engine` workflow artifact after CI succeeds. It contains `H2-Historical-World-3D.zip` with a `kit/` engine root recognized by the existing Multi-Pack installer.

1. In **Projects > Import ZIP H2 / Terrain**, import as a new engine ID such as `historical-world-v1`. Do **not** overwrite your existing engines.
2. In **Environment Manager**, select the new engine and install npm packages, Three.js and Playwright Chromium. Python package bootstrap is not necessary for this engine.
3. Create a new project using pack `historical`. The engine auto-generates 3 test epochs.
4. Use **Check**, **Sheet** and **Full render** in the desktop app. The timeline and scene `args` can be edited through the existing JSON editor.
5. Put narration MP3 files named `s001.mp3`, `s002.mp3`, etc. in `input/voice` before selecting **Mix audio**.

## Project format

```text
project/
  config.json               # title, pack=historical, width, height, fps
  history/world.json        # terrain, epochs, building lifetimes, evidence metadata
  story/shots.json          # timecode, narration, args.epoch, args.camera
  timeline/lines.json       # aligned voice line timestamps
  input/voice/s001.mp3      # optional narration
  build/video_silent.mp4
  build/sheets/sheet_01.jpg
  out/video_final.mp4
```

The renderer never changes the historical world when the video moves between scenes. Instead `activeBuildings(history, epochId)` determines which buildings are visible at a selected year. Scrubbing and FFmpeg frame renders use this same deterministic logic.

## What is intentionally not implemented

- Historical accuracy, historical geography reconstruction, GIS / real DEM and 3D Tiles streaming.
- Advanced procedural city zoning, architectural GLB asset catalog, PBR textures, high-quality water, true foliage shading or interactive asset editing.
- Automatic AI generation of Historical Worlds. Existing AI Terrain Scene Builder is **not reused** for historical.
- Complex historical destruction/building animation interpolation between epochs.
- Photo-realistic video quality comparable to professional Jerusalem timelapses.

This is a **functional MVP** on which asset tooling, evidence handling, historical event deltas, city authoring and AI Historical World Builder can be built in later PRs.
