# H2 V6.2 — Dual-engine world blockout

This PR introduces **two consumers of the same scene-instance JSON**. Both use the same procedural cuboid building geometry, world positions, dimensions, identifiers and base colors. This is a **blockout prototype**, not photorealistic reconstruction or proven screenshot parity.

## Generate shared scene on Windows (CMD)

Run from `engines/h2-historical-world`:

```cmd
python tools\v6_1_world_builder.py --seed 128 --epoch 1200 --output build\v6_1_world.json
python tools\v6_2_scene_contract.py build\v6_1_world.json build\v6_2_scene.json
python -m unittest discover -s tests -p "test_v6_*.py" -v
```

## Three.js interactive preview

Start a static HTTP server in `engines/h2-historical-world`:

```cmd
python -m http.server 8765
```

Open: `http://localhost:8765/tools/v6_2_preview.html?scene=/build/v6_2_scene.json`

This preview requires an internet connection to load Three.js from jsDelivr. The HTTP server is necessary because browsers restrict file:// JSON fetching. Drag to orbit; scroll to zoom.

## Blender still preview

Blender 5.2 example:

```cmd
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b -t 4 --python tools\v6_2_blender_preview.py -- build\v6_2_scene.json build\v6_2_blender.png
```

The Blender script converts canonical Y-up coordinates to Blender Z-up and checks that a nonempty PNG exists. It uses Blender EEVEE, material roughness, a large area light and a basic terrain slab. No external OpenMontage dependency is needed for this proof of concept.

## Acceptance criteria and limitations

- Both engines consume **the same** `build/v6_2_scene.json` and preserve structure IDs and world-space transforms.
- The scene contract rejects duplicate IDs, unsupported kinds, invalid transforms and non-finite values.
- Verify the Blender output PNG and compare structure placement with the browser preview.
- Current preview uses procedural cubes only; GLB/GLTF assets in the V6.1 catalog are **not yet instanced**.
- Roads and rivers are drawn only in Three.js; Blender currently renders buildings and ground. Therefore full visual parity is **not** achieved.
- Both previews use simplified terrain, basic materials and a fixed camera. This is not the final H2 historical render engine.
- CI unit tests do not execute Blender or browser rendering. Complete Windows smoke tests before merging.

## Enhanced V6.2 implementation (architecture, environment, lighting)

This branch now adds deterministic scene details consumed by both renderers:
- Architecture: roof geometry, chimneys/annexes on selected houses, merlons on walls/gates, a gate opening **visual placeholder**, and tower platforms.
- Environment: five conical hills, deterministic low-poly trees, actual road and river ribbon meshes. Terrain is **still a flat ground slab**; the hills are objects and not a heightfield. Roads/rivers do not yet conform to heightfields or carve the terrain.
- Visuals: common daylight configuration, neutral exposure, warmer architectural materials, shadows, three shared camera presets.

**Not yet delivered:** real arch boolean geometry, realistic historical styles, three distinct roof geometries, textured PBR materials, true displaced terrain, bridges, physically based water, GLB asset instancing, animated cameras, or verified image parity. These remain explicit future work, not acceptance claims.

### Updated Windows verification

From `engines\h2-historical-world`:

```cmd
python tools\v6_1_world_builder.py --seed 128 --epoch 1200 --output build\v6_1_world.json
python tools\v6_2_scene_contract.py build\v6_1_world.json build\v6_2_scene.json
python -m unittest discover -s tests -p "test_v6_2_*.py" -v
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b -t 4 --python tools\v6_2_blender_preview.py -- build\v6_2_scene.json build\v6_2_blender.png
```

Reopen the browser preview and hard refresh after regenerating JSON. Compare the two previews manually. The new detailed scene is heavier; Blender may take longer to render.

### Review checklist

- [ ] All V6.2 Python tests pass
- [ ] Blender render produces nonempty PNG with readable color/lighting
- [ ] Browser preview displays matching roofs, walls, road ribbons, river, hills and trees
- [ ] No duplicate scene detail IDs or invalid meshes
- [ ] Camera framing remains consistent
- [ ] Check performance on RTX 3060 12GB
