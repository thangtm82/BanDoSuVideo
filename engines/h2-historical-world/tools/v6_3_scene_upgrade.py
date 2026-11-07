"""V6.3a: upgrade a V6.2 scene with deterministic architectural detail.

Keeps the V6.2 contract renderable by existing Three.js and Blender previews.
No historical-accuracy claim: all added geometry is stylized procedural blockout.
"""
import argparse
import json
import math
from pathlib import Path

def upgrade(scene):
    if scene.get("schema") != "h2.historical_world.preview.v6.2":
        raise ValueError("Expected V6.2 scene input")
    result = json.loads(json.dumps(scene))
    details = result.setdefault("details", [])
    used = {obj["id"] for obj in result["instances"] + details}
    def add(name, shape, pos, size, color, angle=0):
        if name in used:
            raise ValueError("Duplicate generated ID: " + name)
        used.add(name)
        details.append({"id": name, "shape": shape, "position": pos,
                        "size": size, "color": color, "rotation_y": angle, "sides": 8})
    for obj in result["instances"]:
        name, kind = obj["id"], obj["kind"]
        x, y, z = obj["position"]
        w, h, d = obj["dimensions"]
        angle = float(obj.get("rotation_y", 0))
        # Local offsets converted to the same Y-up coordinates used by both engines.
        def place(suffix, lx, ly, lz, size, color, shape="box"):
            px = x + math.cos(angle)*lx + math.sin(angle)*lz
            pz = z - math.sin(angle)*lx + math.cos(angle)*lz
            add(name + suffix, shape, [round(px,4),round(y+ly,4),round(pz,4)],
                size, color, angle)
        if kind in ("house", "market", "temple"):
            # Windows and entrance on the facade; material contrast makes them
            # legible at wide aerial camera angles without requiring textures.
            for side in (-1, 1):
                place("-window-"+str(side), side*w*.25, 0, d*.505,
                      [max(.8,w*.12), max(1.3,h*.22), .25], "#453c33")
            place("-door", 0, -h*.29, d*.51,
                  [max(1.4,w*.19), h*.42, .32], "#594735")
        if kind == "temple":
            for side in (-1, 1):
                place("-column-"+str(side), side*w*.37, -h*.08, d*.55,
                      [1.7,h*.85,1.7], "#d4c3a2")
            place("-pediment", 0, h*.55, d*.54,
                  [w*1.05, 2.0, 2.4], "#c6b18b")
        if kind == "market":
            place("-awning", 0, h*.12, d*.63,
                  [w*.85, .7, d*.32], "#956c4c")
        if kind == "tower":
            for side in (-1, 1):
                place("-slit-"+str(side), side*w*.26, h*.17, d*.51,
                      [.6, h*.23, .28], "#413d36")
    result["version_note"] = "V6.3a architecture detail; V6.2 renderer compatible"
    return result

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("input_scene")
    parser.add_argument("output_scene")
    args = parser.parse_args()
    source = Path(args.input_scene)
    target = Path(args.output_scene)
    scene = upgrade(json.loads(source.read_text(encoding="utf-8-sig")))
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(scene, indent=2, ensure_ascii=False), encoding="utf-8")
    print("V6.3a scene:", target, "objects:", len(scene["details"]))

if __name__ == "__main__":
    main()
