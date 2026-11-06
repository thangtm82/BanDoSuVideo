"""Opt-in OpenMontage tool bridge. Never modifies the existing H2 renderer."""
from __future__ import annotations
import argparse
import importlib
import json
import os
import sys
from pathlib import Path

ALLOWED = {
    "threejs_world": "tools.graphics.threejs_world",
    "blender_world": "tools.graphics.blender_world",
    "threejs_asset_catalog": "tools.graphics.threejs_asset_catalog",
}
def invoke(repo_path, tool_name, payload):
    root = Path(repo_path).expanduser().resolve()
    if not (root / "tools" / "base_tool.py").is_file():
        raise ValueError("OpenMontage source not found (tools/base_tool.py)")
    if tool_name not in ALLOWED:
        raise ValueError("Tool not allowlisted")
    if not isinstance(payload, dict):
        raise ValueError("Payload must be a JSON object")
    sys.path.insert(0, str(root))
    module = importlib.import_module(ALLOWED[tool_name])
    cls_name = {"threejs_world": "ThreeJSWorld", "blender_world": "BlenderWorld", "threejs_asset_catalog": "ThreeJSAssetCatalog"}[tool_name]
    tool = getattr(module, cls_name)()
    result = tool.execute(payload)
    return {"success": bool(result.success), "error": result.error, "data": result.data, "artifacts": result.artifacts}

def main():
    p = argparse.ArgumentParser()
    p.add_argument("--repo", default=os.environ.get("OPENMONTAGE_HOME"))
    p.add_argument("--tool", choices=sorted(ALLOWED), required=True)
    p.add_argument("--input", type=Path, required=True)
    p.add_argument("--output", type=Path)
    args = p.parse_args()
    try:
        if not args.repo:
            raise ValueError("Set OPENMONTAGE_HOME or provide --repo")
        data = invoke(args.repo, args.tool, json.loads(args.input.read_text(encoding="utf-8-sig")))
    except Exception as exc:
        data = {"success": False, "error": str(exc), "data": {}, "artifacts": []}
    result = json.dumps(data, ensure_ascii=False, indent=2, default=str)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(result, encoding="utf-8")
    print(result)
    raise SystemExit(0 if data["success"] else 1)

if __name__ == "__main__":
    main()
