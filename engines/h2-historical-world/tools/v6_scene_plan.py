"""H2 V6 scene plan validation and conversion. No third-party dependencies."""
from __future__ import annotations
import argparse
import json
from pathlib import Path

SCENE_TYPES = {"world3d", "terrain3d", "map2d", "footage", "graphic"}
RENDERERS = {"threejs", "blender", "existing"}
def validate(plan):
    errors = []
    if not isinstance(plan, dict) or not isinstance(plan.get("scenes"), list):
        return ["Plan must be an object containing scenes[]"]
    worlds = plan.get("worlds", [])
    if not isinstance(worlds, list):
        return ["worlds must be an array"]
    world_ids = [w.get("id") for w in worlds if isinstance(w, dict)]
    if len(world_ids) != len(set(world_ids)):
        errors.append("Duplicate world IDs")
    scene_ids = set()
    for i, scene in enumerate(plan["scenes"]):
        prefix = f"scenes[{i}]"
        if not isinstance(scene, dict):
            errors.append(f"{prefix} must be an object")
            continue
        sid = scene.get("id")
        if not isinstance(sid, str) or not sid:
            errors.append(f"{prefix}.id is required")
        elif sid in scene_ids:
            errors.append(f"{prefix}.id is duplicated: {sid}")
        scene_ids.add(sid)
        typ = scene.get("type")
        if typ not in SCENE_TYPES:
            errors.append(f"{prefix}.type must be one of {sorted(SCENE_TYPES)}")
        start, end = scene.get("start_seconds"), scene.get("end_seconds")
        if not isinstance(start, (int, float)) or isinstance(start, bool) or not isinstance(end, (int, float)) or isinstance(end, bool) or start < 0 or end <= start:
            errors.append(f"{prefix} requires 0 <= start_seconds < end_seconds")
        if typ in {"world3d", "terrain3d"} and scene.get("world_id") not in world_ids:
            errors.append(f"{prefix}.world_id must reference worlds[]")
        if scene.get("renderer", "existing") not in RENDERERS:
            errors.append(f"{prefix}.renderer is invalid")
        if not scene.get("script_section_id"):
            errors.append(f"{prefix}.script_section_id is required")
    return errors

def to_openmontage_scene_plan(plan):
    """Produce OpenMontage scene_plan v1.0 without injecting unsupported fields."""
    errors = validate(plan)
    if errors:
        raise ValueError("; ".join(errors))
    scenes = []
    for scene in plan["scenes"]:
        camera = scene.get("camera") or {}
        scenes.append({
            "id": scene["id"],
            "type": "animation" if scene["type"] in {"world3d", "terrain3d", "map2d", "graphic"} else "broll",
            "description": scene.get("description", ""),
            "start_seconds": scene["start_seconds"],
            "end_seconds": scene["end_seconds"],
            "script_section_id": scene["script_section_id"],
            "framing": camera.get("shot_size", "wide"),
            "movement": camera.get("movement", "static"),
            "shot_intent": scene.get("visual_intent", ""),
            "required_assets": [{"type": "world", "description": scene["world_id"], "source": "provided"}] if scene.get("world_id") else []
        })
    return {"version": "1.0", "scenes": scenes, "metadata": {"h2_worlds": plan.get("worlds", []), "h2_scene_refs": [{"id": s["id"], "world_id": s.get("world_id"), "renderer": s.get("renderer", "existing")} for s in plan["scenes"]]}}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("plan", type=Path)
    parser.add_argument("--openmontage-output", type=Path)
    args = parser.parse_args()
    plan = json.loads(args.plan.read_text(encoding="utf-8-sig"))
    errors = validate(plan)
    if errors:
        for error in errors:
            print("ERROR:", error)
        raise SystemExit(1)
    print(f"Valid H2 V6 scene plan: {len(plan['scenes'])} scenes")
    if args.openmontage_output:
        args.openmontage_output.parent.mkdir(parents=True, exist_ok=True)
        args.openmontage_output.write_text(json.dumps(to_openmontage_scene_plan(plan), ensure_ascii=False, indent=2), encoding="utf-8")
        print("Wrote", args.openmontage_output)

if __name__ == "__main__":
    main()
