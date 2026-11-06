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
