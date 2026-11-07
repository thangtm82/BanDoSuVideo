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

## V6.3b/c experimental implementation (added)

The first V6.3b/c code slices are committed, but **not yet fully production-ready**:
- `tools/v6_3_natural_world.py`: shared regular-grid heightfield, river depression and simple bridge deck. Both V6.2 renderers now accept `environment.terrain_mesh`.
- `tools/v6_3_cinematic.py`: deterministic orbital camera plan and FFmpeg MP4 assembly.
- `tools/v6_3_blender_animation.py`: renders sequential PNG frames using the same scene as the Blender still renderer.
- `tests/test_v6_3_natural_cinematic.py`: geometry and cinematic planning regression tests.

### Windows end-to-end commands

From `engines\h2-historical-world` after running the V6.3a commands above:

```cmd
python tools\v6_3_natural_world.py build\v6_3_scene.json build\v6_3_natural.json
python tools\v6_2_validate_scene.py build\v6_3_natural.json
python -m unittest discover -s tests -p "test_v6_3_*.py" -v
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b -t 4 --python tools\v6_2_blender_preview.py -- build\v6_3_natural.json build\v6_3_natural.png bright_final
python tools\v6_3_cinematic.py plan build\v6_3_natural.json build\v6_3_camera.json --frames 120 --fps 24
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b -t 4 --python tools\v6_3_blender_animation.py -- build\v6_3_natural.json build\v6_3_camera.json build\v6_3_frames bright_final
python tools\v6_3_cinematic.py assemble build\v6_3_frames build\v6_3_cinematic.mp4 --fps 24
```

Three.js scene: `http://localhost:8765/tools/v6_2_preview.html?scene=/build/v6_3_natural.json` after `python -m http.server 8765`.

### Known limitations

- Bridge deck is an approximate primitive at the middle river point, not guaranteed to coincide with a road crossing; gate/wall and river collisions remain.
- Roads are still planar overlays and can intersect new hills; vegetation is not yet terrain-snapped.
- River surface remains a flat ribbon, not a fully conforming water body.
- Three.js camera path playback is not yet implemented; use the Blender renderer for animation.
- Blender animation renders a setup still before the frames, adding overhead.
- The pipeline has not been executed in a Blender/FFmpeg environment by the GitHub connector. Verify visually and run all tests before marking PR ready.

## V6.3c camera revision after first MP4 review

Camera path now has three continuous phases: establishing (0–38%), approach
(38–76%), and flyby (76–100%). Smoothstep is used within each phase.
This is a new camera plan only; **regenerate the plan JSON** before rerendering.

```cmd
python tools\v6_3_cinematic.py plan build\v6_3_natural.json build\v6_3_camera.json --frames 240 --fps 24
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b -t 4 --python tools\v6_3_blender_animation.py -- build\v6_3_natural.json build\v6_3_camera.json build\v6_3_frames_10s bright_final
python tools\v6_3_cinematic.py assemble build\v6_3_frames_10s build\v6_3_cinematic_10s.mp4 --fps 24
```

Use a **fresh frames directory** to avoid mixing 120-frame and 240-frame
renders. The natural-world bridge is still a primitive; river/wall intersections
are not fully resolved. This branch is still draft pending render QA.
