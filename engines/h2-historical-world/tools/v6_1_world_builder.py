"""V6.1: deterministic historical-world scene manifest from the existing city generator.

Produces a canonical scene graph (not a rendered Blender/Three.js world).
Units are metres, Y is up; asset references are explicit, no implicit downloads.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import math
from pathlib import Path
from city_generator import create_city, RIVER

KINDS = {"house", "market", "temple", "tower", "wall", "gate"}
CAMERAS = [
    {"id": "establishing", "position": [520, 260, 500], "target": [0, 0, 0], "fov": 45},
    {"id": "market", "position": [90, 75, 100], "target": [-54, 5, 15], "fov": 50},
    {"id": "fortifications", "position": [360, 120, 300], "target": [240, 10, 0], "fov": 48},
]

def safe_asset(path, root):
    if not isinstance(path, str) or not path.lower().endswith((".glb", ".gltf")):
        raise ValueError("Asset must reference a .glb or .gltf file")
    candidate = (root / path).resolve()
    if not candidate.is_relative_to(root.resolve()):
        raise ValueError("Asset path escapes asset root")
    if not candidate.is_file():
        raise FileNotFoundError("Missing asset: " + str(candidate))
    return candidate

def validate_catalog(catalog, root):
    if not isinstance(catalog, dict):
        raise ValueError("Asset catalog must be a JSON object")
    result = {}
    for kind, entry in catalog.items():
        if kind not in KINDS or not isinstance(entry, dict):
            raise ValueError("Unknown asset kind or invalid entry: " + str(kind))
        path = entry.get("path")
        file = safe_asset(path, root)
        license_id = entry.get("license")
        source = entry.get("source")
        if not isinstance(license_id, str) or not license_id.strip() or not isinstance(source, str) or not source.strip():
            raise ValueError("Asset requires nonempty license and source: " + kind)
        result[kind] = {
            "path": path.replace("\\", "/"),
            "license": license_id,
            "source": source,
            "sha256": hashlib.sha256(file.read_bytes()).hexdigest(),
        }
    return result

def build_world(seed=128, epoch=1200, catalog=None, asset_root=None):
    if not isinstance(seed, int) or isinstance(seed, bool):
        raise ValueError("seed must be an integer")
    if not isinstance(epoch, int) or isinstance(epoch, bool):
        raise ValueError("epoch must be an integer")
    assets = validate_catalog(catalog or {}, Path(asset_root or "."))
    buildings, roads, events = create_city(seed)
    nodes = []
    for b in buildings:
        if b["from"] > epoch:
            continue
        if b["kind"] not in KINDS:
            raise ValueError("Unknown generated building kind: " + b["kind"])
        x, z = b["p"]
        rotation = float(b.get("rotation", 0))
        scale = float(b.get("scale", 1))
        nodes.append({
            "id": b["id"], "kind": b["kind"], "variant": b.get("variant", b["kind"]),
            "position": [x, 0.0, z], "rotation_y": rotation,
            "scale": [scale, scale, scale], "asset_kind": b["kind"] if b["kind"] in assets else None,
            "built_from": b["from"],
        })
    if len({node["id"] for node in nodes}) != len(nodes):
        raise ValueError("Duplicate scene node IDs")
    return {
        "schema": "h2.historical_world.v6.1",
        "world_id": "synthetic-historical-town",
        "evidence_status": "fictional_not_historically_verified",
        "seed": seed, "epoch": epoch, "units": "meters", "up_axis": "Y",
        "terrain": {"extent": 1200, "seed": seed, "material": "terrain-placeholder"},
        "river": [{"position": [x, 0, z]} for x, z in RIVER],
        "roads": [{"id": r["id"], "points": [[x, 0, z] for x, z in r["pts"]], "kind": r["kind"]}
                  for r in roads if r["from"] <= epoch],
        "nodes": nodes,
        "asset_catalog": assets,
        "cameras": CAMERAS,
        "events": [e for e in events if e["year"] <= epoch],
        "render_contract": {"resolution": [1280, 720], "fps": 24, "preview": "threejs", "final": "blender"},
    }

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--seed", type=int, default=128)
    p.add_argument("--epoch", type=int, default=1200)
    p.add_argument("--asset-catalog", type=Path)
    p.add_argument("--asset-root", type=Path, default=Path("."))
    p.add_argument("--output", type=Path, required=True)
    args = p.parse_args()
    catalog = json.loads(args.asset_catalog.read_text(encoding="utf-8-sig")) if args.asset_catalog else {}
    world = build_world(args.seed, args.epoch, catalog, args.asset_root)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(world, indent=2, ensure_ascii=False), encoding="utf-8")
    print("Wrote", args.output, "nodes:", len(world["nodes"]), "roads:", len(world["roads"]))

if __name__ == "__main__":
    main()
