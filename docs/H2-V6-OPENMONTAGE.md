# H2 Historical World 3D V6 — OpenMontage integration (experimental)

This first integration is **opt-in**. It does not replace `render.mjs`, rewrite legacy projects, or install OpenMontage automatically.

## Implemented

- `engines/h2-historical-world/tools/v6_scene_plan.py`: validates H2-specific scene/world references and converts to OpenMontage scene_plan v1.0, with H2 metadata retained.
- `engines/h2-historical-world/tools/v6_openmontage_bridge.py`: allowlisted CLI adapter for OpenMontage `threejs_world`, `blender_world`, `threejs_asset_catalog`.
- Unit tests for validation and conversion.

## Windows setup

Install OpenMontage separately from https://github.com/calesthio/OpenMontage and install its Python dependencies. Set `OPENMONTAGE_HOME` to that checkout. Blender 4.5 LTS is optional for the Blender tool; set `BLENDER_PATH` if necessary. HyperFrames final render requires Node.js 22+, npx, FFmpeg and a resolvable HyperFrames package.

```powershell
cd engines/h2-historical-world
python -m unittest discover -s tests -p "test_v6_integration.py"
python tools/v6_scene_plan.py examples/v6_scene_plan.json --openmontage-output build/v6_openmontage_scene_plan.json
$env:OPENMONTAGE_HOME = "D:\\OpenMontage"
python tools/v6_openmontage_bridge.py --tool threejs_world --input examples/v6_threejs_validate.json --output build/v6_world_validation.json
```

For `threejs_world`, use `operation=validate` before `build`. For Blender, use `operation=doctor` before rendering. The bridge passes tool-specific JSON through without silently changing quality tiers or render runtimes. Outputs are saved as JSON and should be reviewed before approving render.

## Deliberate limits

This PR does **not** implement Blender/Three.js scene equivalence, GLB/PBR asset acquisition, a C# UI, background job management, automated voice mixing, or a full production pipeline. OpenMontage `scene_plan` does not natively model H2 world IDs; these are preserved in `metadata.h2_scene_refs` and must be retained when round-tripping.

No historical claims are inferred from generated geometry. Do not distribute OpenMontage-derived source without reviewing AGPL-3.0 obligations.
