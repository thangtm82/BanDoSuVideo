"""Adapt a ThreeJSWorld normalized spec for OpenMontage BlenderWorld.

Geometry is NOT equivalent: the Blender terrain algorithm differs. GLB assets
must be supplied separately. No Blender or OpenMontage dependency required.
"""
from __future__ import annotations
import json
from pathlib import Path

def rgba(value):
    if isinstance(value, str):
        text = value.lstrip("#")
        if len(text) not in (6, 8):
            raise ValueError("Expected #RRGGBB or #RRGGBBAA")
        return [int(text[i:i+2], 16) / 255.0 for i in range(0, len(text), 2)] + ([1.0] if len(text) == 6 else [])
    if isinstance(value, (list, tuple)) and len(value) in (3, 4):
        if not all(isinstance(x, (int, float)) and not isinstance(x, bool) and 0 <= x <= 1 for x in value):
            raise ValueError("RGBA components must be numeric in [0,1]")
        return list(value) + ([1.0] if len(value) == 3 else [])
    raise ValueError("Invalid color")

def convert(three):
    if not isinstance(three, dict):
        raise ValueError("world_spec must be an object")
    world = three.get("world") or {}
    size = float(world.get("size", 240))
    if size <= 0:
        raise ValueError("world.size must be positive")
    terrain = {
        "size": size,
        "resolution": max(32, min(320, int(world.get("resolution", 180)))),
        "height_scale": float(world.get("elevation_scale", 16)),
        "palette": [rgba(c) for c in ("#6b5940", "#8c7650", "#4d5740")],
    }
    regions = []
    for item in three.get("regions", []):
        if not isinstance(item, dict):
            raise ValueError("regions entries must be objects")
        center = item.get("center", [0, 0])
        if not isinstance(center, list) or len(center) < 2:
            raise ValueError("region.center must be a 2-vector")
        region = {
            "id": str(item.get("id", "region")),
            "center": [float(center[0]) * size / 2, float(center[1]) * size / 2],
            "radius": max(1.0, float(item.get("radius", 0.75)) * size / 2),
            "color": rgba(item.get("color", "#8c7650")),
            "roughness": 0.88,
        }
        regions.append(region)
    # Three.js uses X/Y(up)/Z; Blender uses X/Y/Z(up).
    camera_path = []
    for key in three.get("camera_path", []):
        def xyz(v):
            if not isinstance(v, list) or len(v) != 3:
                raise ValueError("camera position/target must be 3-vectors")
            return [float(v[0]), float(v[2]), float(v[1])]
        camera_path.append({
            "time": float(key["time"]),
            "position": xyz(key["position"]),
            "target": xyz(key["target"]),
        })
    camera = {"path": camera_path}
    if camera_path:
        camera["position"] = camera_path[0]["position"]
        camera["target"] = camera_path[0]["target"]
    return {
        "seed": int(three.get("seed", 42)),
        "terrain": terrain,
        "regions": regions,
        "camera": camera,
        "assets": [],
        "_h2_adapter_note": "Blockout only; landmarks and GLB assets require explicit mapping.",
    }

def main():
    import argparse
    p = argparse.ArgumentParser()
    p.add_argument("source", type=Path)
    p.add_argument("destination", type=Path)
    args = p.parse_args()
    obj = json.loads(args.source.read_text(encoding="utf-8-sig"))
    spec = obj.get("world_spec", obj)
    result = convert(spec)
    args.destination.parent.mkdir(parents=True, exist_ok=True)
    args.destination.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(args.destination)

if __name__ == "__main__":
    main()
