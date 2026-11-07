# H2 Historical World V6.3 — implementation plan

Base: V6.2 merged to main via PR #25. New work lives in an isolated feature branch and starts with **V6.3a**.

## V6.3a Historical Architecture — implemented initial slice

- Deterministic building facade additions: dark window apertures, doors, market awnings, temple columns/pediment, tower slits.
- Uses existing V6.2 box primitives so both Three.js and Blender can render the same data without changing renderer code.
- Non-destructive: input scene is unchanged, output includes new details and a version note.
- Unit tests cover determinism, architecture, schema and duplicate IDs.
- This is a procedural approximation, not a historical reconstruction.

## V6.3b Natural World — next slice

- Replace cone hills with sampled terrain heightfield in a shared mesh schema.
- Route roads and rivers to terrain elevations; resolve intersections with walls.
- Add bridges and gates with traversable openings, riverbanks and terrain-aware vegetation.
- Tests: path continuity, mesh bounds, collision, deterministic seed, parity across renderers.

## V6.3c Cinematic Rendering — next slice

- Shared camera keyframes and interpolated trajectories.
- Blender image-sequence rendering and FFmpeg MP4 assembly with error handling.
- Three.js camera path preview, output manifests and render quality presets.
- Tests: frame counts, camera orientation, output files, codec and command validation.

## Windows test V6.3a

From `engines\h2-historical-world`:

```cmd
python tools\v6_1_world_builder.py --seed 128 --epoch 1200 --output build\v6_1_world.json
python tools\v6_2_scene_contract.py build\v6_1_world.json build\v6_2_scene.json
python tools\v6_3_scene_upgrade.py build\v6_2_scene.json build\v6_3_scene.json
python tools\v6_2_validate_scene.py build\v6_3_scene.json
python -m unittest discover -s tests -p "test_v6_3_*.py" -v
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b -t 4 --python tools\v6_2_blender_preview.py -- build\v6_3_scene.json build\v6_3_blender.png bright_final
python -m http.server 8765
```

Three.js: `http://localhost:8765/tools/v6_2_preview.html?scene=/build/v6_3_scene.json`.

## Release gate

Do not mark ready or merge until V6.3b/c are implemented and tests/render evidence reviewed. No automated Blender or browser render was executed by this GitHub-only implementation.
