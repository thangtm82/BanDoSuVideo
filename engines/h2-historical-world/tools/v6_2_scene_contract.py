"""V6.2 shared world contract and deterministic procedural placeholder dimensions.

Y-up, metres, identical transforms in Three.js and Blender (Z-up conversion).
"""
from __future__ import annotations
import json
import math
from pathlib import Path

SCHEMA = "h2.historical_world.v6.1"
DIMENSIONS = {
    "house": (11, 9, 13), "market": (22, 7, 18),
    "temple": (17, 18, 22), "tower": (9, 23, 9),
    "wall": (32, 13, 5), "gate": (22, 18, 9),
}
COLORS = {
    "house": "#b89a78", "market": "#b99c73", "temple": "#c7b99a",
    "tower": "#938777", "wall": "#a99a83", "gate": "#b8a78c",
}

def validate_world(world):
    if not isinstance(world, dict) or world.get("schema") != SCHEMA:
        raise ValueError("Expected h2.historical_world.v6.1 manifest")
    if world.get("up_axis") != "Y" or world.get("units") != "meters":
        raise ValueError("Expected Y-up world in meters")
    nodes = world.get("nodes")
    if not isinstance(nodes, list):
        raise ValueError("nodes must be an array")
    ids = set()
    for node in nodes:
        if not isinstance(node, dict) or not isinstance(node.get("id"), str) or not node["id"]:
            raise ValueError("Invalid node ID")
        if node["id"] in ids:
            raise ValueError("Duplicate node ID: " + node["id"])
        ids.add(node["id"])
        if node.get("kind") not in DIMENSIONS:
            raise ValueError("Unknown node kind: " + str(node.get("kind")))
        for key, length in (("position", 3), ("scale", 3)):
            value = node.get(key)
            if not isinstance(value, list) or len(value) != length or any(
                not isinstance(x, (int, float)) or isinstance(x, bool) or not math.isfinite(x) for x in value
            ):
                raise ValueError("Invalid " + key + " for " + node["id"])
        if any(x <= 0 for x in node["scale"]):
            raise ValueError("Nonpositive scale for " + node["id"])
        angle = node.get("rotation_y")
        if not isinstance(angle, (int, float)) or isinstance(angle, bool) or not math.isfinite(angle):
            raise ValueError("Invalid rotation_y for " + node["id"])
    return world

def instances(world):
    validate_world(world)
    output = []
    for node in world["nodes"]:
        dims = DIMENSIONS[node["kind"]]
        scale = node["scale"]
        height = dims[1] * scale[1]
        output.append({
            "id": node["id"], "kind": node["kind"], "asset_kind": node.get("asset_kind"),
            "position": [node["position"][0], node["position"][1] + height / 2, node["position"][2]],
            "rotation_y": node["rotation_y"], "dimensions": [dims[i] * scale[i] for i in range(3)],
            "color": COLORS[node["kind"]],
        })
    return output

def write_scene(world_path, output_path):
    world = json.loads(Path(world_path).read_text(encoding="utf-8-sig"))
    payload = {"schema": "h2.historical_world.preview.v6.2", "instances": instances(world),
               "roads": world.get("roads", []), "river": world.get("river", []),
               "cameras": world.get("cameras", []), "asset_catalog": world.get("asset_catalog", {}),
               "evidence_status": world.get("evidence_status")}
    target = Path(output_path)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
    return payload

if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument("world")
    parser.add_argument("output")
    args = parser.parse_args()
    scene = write_scene(args.world, args.output)
    print("V6.2 instances:", len(scene["instances"]), "=>", args.output)
