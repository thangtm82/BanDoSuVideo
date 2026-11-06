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

## V6 readiness updates

- `tools/v6_world_spec_adapter.py` maps Three.js normalized region colors (HEX to float RGBA), region center/radius and camera XYZ (Three.js Y-up to Blender Z-up) to BlenderWorld's schema. This is a **blockout-only adapter**: BlenderWorld uses a different terrain algorithm; landmarks and buildings are **not** automatically transferred.
- The H2 bridge now checks actual Blender output files for build and still operations, preventing a false-positive success when Blender exits 0 after a Python traceback. Animation checks use an output prefix and should be verified against the concrete frame naming convention before production use.
- `.github/workflows/h2-v6-integration-tests.yml` runs Python syntax and unit regression tests without Blender, network providers, or paid APIs.

### Windows smoke test (CMD)

```cmd
cd /d D:\OpenMontage
python -m unittest discover -s D:\BanDoSuVideo\engines\h2-historical-world\tests -p "test_v6_*.py" -v
```

The H2 path above is illustrative. Set `BLENDER_PATH` to your installed Blender executable. On Blender 5.2.x, upstream OpenMontage's `tools/graphics/templates/blender-world-runtime.py` may require a local compatibility patch: use `BLENDER_EEVEE` rather than `BLENDER_EEVEE_NEXT`, and only set `scene.eevee.taa_render_samples` if available. **This PR does not modify the external OpenMontage checkout.** Re-run build and still-render and verify the .blend and PNG artifacts. For the first smoke test, use 720p, 16 samples.

### Review scope

Ready for review means the integration code is reviewable; it does **not** mean the full historical cinematic pipeline, Three.js/Blender visual parity, or production-grade historical assets are finished. Do not merge solely because this PR is marked ready.
