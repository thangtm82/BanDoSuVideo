"""Validate V6.2 final scene before launching Three.js or Blender."""
import argparse
import json
import math
from pathlib import Path

def validate(scene):
    if scene.get("schema") != "h2.historical_world.preview.v6.2":
        raise ValueError("Unsupported V6.2 scene schema")
    instances = scene.get("instances")
    details = scene.get("details")
    if not isinstance(instances, list) or not instances:
        raise ValueError("Scene must contain instances")
    if not isinstance(details, list) or not details:
        raise ValueError("Scene must contain procedural details; regenerate JSON")
    names = set()
    for item in instances + details:
        name = item.get("id")
        if not isinstance(name, str) or not name or name in names:
            raise ValueError("Missing or duplicate object ID: " + str(name))
        names.add(name)
        for key in ("position", "dimensions" if item in instances else "size"):
            value = item.get(key)
            if not isinstance(value, list) or len(value) != 3 or any(
                not isinstance(x, (int, float)) or isinstance(x, bool) or not math.isfinite(x)
                for x in value
            ):
                raise ValueError("Invalid " + key + " for " + name)
        dims = item.get("dimensions", item.get("size"))
        if any(x <= 0 for x in dims):
            raise ValueError("Nonpositive dimensions for " + name)
    env = scene.get("environment", {})
    if not isinstance(env.get("roads"), list):
        raise ValueError("Missing shared roads")
    if not isinstance(env.get("river"), dict):
        raise ValueError("Missing shared river")
    if not scene.get("cameras"):
        raise ValueError("Missing camera")
    return {"instances": len(instances), "details": len(details),
            "roads": len(env["roads"]), "river_points": len(env["river"].get("points", []))}

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("scene_json")
    args = parser.parse_args()
    result = validate(json.loads(Path(args.scene_json).read_text(encoding="utf-8-sig")))
    print("V6.2 FINAL scene valid:", result)
