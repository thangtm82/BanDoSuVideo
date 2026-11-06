# H2 V6.1 — Historical World Builder (prototype)

This PR introduces a **deterministic, inspectable world manifest** for the existing synthetic historical town. It does not claim historical accuracy or render a production-quality 3D city.

## Implemented
- Reuses `tools/city_generator.py` rather than creating another town-layout algorithm.
- Emits `h2.historical_world.v6.1` JSON with unique scene nodes, road polylines, river waypoints, epoch, three named camera viewpoints and an explicit Y-up, metre-scale contract.
- Supports opt-in local GLB/GLTF asset references, SHA-256 fingerprints, source attribution and license metadata. Missing files, missing license metadata and directory traversal are rejected.
- Deterministic regression tests for epoch filtering, camera count, scene IDs and asset metadata.
- Keeps the existing H2 render pipeline unchanged.

## Windows quick start

```cmd
cd /d D:\BanDoSuVideo\engines\h2-historical-world
python tools\v6_1_world_builder.py --seed 128 --epoch 1200 --output build\v6_1_world.json
python -m unittest discover -s tests -p "test_v6_1_*.py" -v
```

Use `--asset-catalog assets.json --asset-root D:\MyAssets` to bind local GLB/GLTF assets. Example catalog:

```json
{
  "house": {
    "path": "houses/house01.glb",
    "license": "CC0-1.0",
    "source": "Artist or collection URL"
  }
}
```

## Important limitations and follow-up
- This PR does **not** automatically create meshes or transfer Three.js scenes into Blender.
- `asset_kind` points to a catalog entry; actual instancing and per-variant asset selection are deferred.
- Roads and river are geometric planning data, not rendered meshes.
- The world is fictional and does not represent a verified historical settlement.
- Follow-up V6.2 should consume this exact manifest in **both** Three.js and Blender, preserving building IDs and transforms, and then add real asset instances, PBR materials, render verification and screenshot comparison.

## Acceptance for this PR
- Python unit tests pass on CI.
- Repeated runs with the same seed and epoch produce byte-identical JSON.
- At least 30 scene nodes and 3 camera viewpoints are present.
- No modifications to legacy `render.mjs` or existing project schemas.
