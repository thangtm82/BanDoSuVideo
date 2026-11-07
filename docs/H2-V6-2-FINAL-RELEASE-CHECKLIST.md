# H2 Historical World 3D — V6.2 Final candidate

## Scope

V6.2 is a **dual-engine procedural preview**, not a historically verified or photorealistic reconstruction. Both Three.js and Blender use one generated scene JSON with shared structure positions, roofs, details, roads, river and basic environment. Final candidate adds a calibrated `bright_final` lighting preset, and a pre-render JSON validator.

### Lighting presets

| Preset | World | Sun | Fill | Exposure | Sun angle |
|---|---:|---:|---:|---:|---:|
| bright_final (default) | 0.55 | 3.0 | 12000 | +0.15 | 3.5° |
| daylight | 0.50 | 2.6 | 12000 | +0.10 | 2.5° |
| bright | 0.65 | 3.1 | 18000 | +0.20 | 2.5° |
| dramatic | 0.30 | 2.9 | 5000 | 0.0 | 2.5° |

## Windows CMD — complete verification

Run from `engines\h2-historical-world` after updating the PR branch:

```cmd
python tools\v6_1_world_builder.py --seed 128 --epoch 1200 --output build\v6_1_world.json
python tools\v6_2_scene_contract.py build\v6_1_world.json build\v6_2_scene.json
python tools\v6_2_validate_scene.py build\v6_2_scene.json
python -m unittest discover -s tests -p "test_v6_2_*.py" -v
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b -t 4 --python tools\v6_2_blender_preview.py -- build\v6_2_final.png bright_final
```

To preview Three.js:

```cmd
python -m http.server 8765
```

Open `http://localhost:8765/tools/v6_2_preview.html?scene=/build/v6_2_scene.json` and refresh with Ctrl+F5.

## Acceptance gate

- [ ] All Python tests pass
- [ ] Scene validator reports a nonzero number of instances and details
- [ ] Blender 5.2 produces a nonempty `build/v6_2_final.png`
- [ ] Bright Final has readable shadows and preserves roof/ground/stone colors
- [ ] Three.js preview loads the same JSON without console errors
- [ ] Review both screenshots for matching positions, scale, roofs and roads
- [ ] Check no regressions in legacy H2 engine

## Deferred beyond V6.2

True sculpted heightfield, carved river channel, navigable bridges, physical gate arch, PBR textures, GLB historical assets, animated cinematic camera, automated image parity, and historically accurate architectural styles are **not** included. Do not describe the current preview as production-quality or fully historically reconstructed.
