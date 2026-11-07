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
            "roof": {
                "height": round(max(2.0, dims[1] * scale[1] * 0.38), 3),
                "color": "#855b48" if node["kind"] in ("house", "market") else "#746655",
                "style": "hip" if node["kind"] in ("house", "market", "temple", "tower") else "none",
            },
        })
    return output

def environment(world):
    """Shared Y-up, metre-based geometry descriptors for both renderers."""
    roads = []
    for road in world.get("roads", []):
        points = road.get("points", [])
        if len(points) >= 2:
            roads.append({"id": road["id"], "points": points, "width": 5.0,
                          "color": "#aa9678"})
    river = [item["position"] for item in world.get("river", [])]
    return {
        "roads": roads,
        "river": {"points": river, "width": 14.0, "color": "#4b899d"},
        "lighting": {"sun_energy": 2.2, "ambient_strength": 0.7,
                     "exposure": 0.0, "ground_color": "#758265"},
    }

def architectural_details(world):
    """Engine-neutral primitives: box and cone, canonical Y-up/metres."""
    result = []
    def add(id, shape, position, size, color, rotation_y=0.0, sides=8):
        result.append({"id": id, "shape": shape, "position": position,
                       "size": size, "color": color, "rotation_y": rotation_y,
                       "sides": sides})
    for inst in instances(world):
        x, y, z = inst["position"]
        dx, dy, dz = inst["dimensions"]
        kind = inst["kind"]
        angle = inst["rotation_y"]
        if kind == "house":
            # Reproducible architectural variation, without Python hash randomization.
            variant = sum(ord(ch) for ch in inst["id"]) % 3
            if variant == 0:
                add(inst["id"]+"-chimney", "box",
                    [x + dx*0.23, y+dy*0.5+2, z], [2.0, 4.0, 2.0], "#786454", angle)
            elif variant == 1:
                add(inst["id"]+"-annex", "box",
                    [x+dx*0.42, y-dy*0.2, z], [dx*0.32, dy*0.55, dz*0.55], "#a48a6e", angle)
        if kind in ("wall", "gate"):
            # Battlements along wall's local X axis.
            count = max(2, int(dx // 5))
            for k in range(count):
                offset = (k-(count-1)/2) * dx / count
                add(inst["id"]+"-merlon-"+str(k), "box",
                    [x+math.cos(angle)*offset, y+dy/2+1.3,
                     z-math.sin(angle)*offset], [dx/count*0.55, 2.6, 3.5],
                    "#b2a38d", angle)
        if kind == "gate":
            add(inst["id"]+"-arch-dark", "box", [x, y-dy*0.22, z+dz/2+0.15],
                [dx*0.3, dy*0.55, 0.35], "#514c42", angle)
        if kind == "tower":
            add(inst["id"]+"-watch-platform", "box",
                [x, y+dy*0.5+0.4, z], [dx*1.18, 1.0, dz*1.18], "#b4a38c", angle)
    # Environment objects are shared by both engines; hill cones are distant
    # background landmarks, not a heightfield or terrain displacement.
    for i, (x, z, r, h) in enumerate([
        (-440,-380,95,48), (-300,-450,75,32), (350,-420,110,55),
        (430,320,90,40), (-440,350,80,38)]):
        add("hill-"+str(i), "cone", [x,h/2,z], [r*2,h,r*2], "#788568", sides=16)
    # Seeded tree positions outside the central town square.
    seed = int(world.get("seed", 128))
    for i in range(70):
        x = ((i*197 + seed*17) % 1060) - 530
        z = ((i*311 + seed*13) % 1060) - 530
        if abs(x) < 290 and abs(z) < 290:
            continue
        add("tree-"+str(i)+"-trunk", "box", [x,4,z], [2.2,8,2.2], "#6e5c43")
        add("tree-"+str(i)+"-canopy", "cone", [x,13,z], [13,20,13], "#506e4a", sides=7)
    return result

def write_scene(world_path, output_path):
    world = json.loads(Path(world_path).read_text(encoding="utf-8-sig"))
    payload = {"schema": "h2.historical_world.preview.v6.2", "instances": instances(world),
               "roads": world.get("roads", []), "river": world.get("river", []),
               "cameras": world.get("cameras", []), "asset_catalog": world.get("asset_catalog", {}),
               "evidence_status": world.get("evidence_status"),
               "environment": environment(world), "details": architectural_details(world)}
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
