"""Generate a reviewable historical scene plan from a script using an OpenAI-compatible API.

No silent fallbacks: a missing API key, malformed response or invalid schema fails.
The output is a DRAFT, not a renderable V6.2 scene.
"""
import argparse
import json
import os
import urllib.request
from pathlib import Path

SCHEMA = "h2.historical_world.scene_plan.v6.4"
SYSTEM = """You are a historical visualization planner. Return only JSON, no markdown.
Format: {"schema":"h2.historical_world.scene_plan.v6.4",
"title":"...","status":"draft_requires_review","shots":[
{"id":"shot-001","narration":"...","location":"...","era":"...",
"visual_description":"...","camera":"wide aerial","duration_seconds":8,
"historical_uncertainties":["..."]}]}.
Do not invent precise historical facts; flag uncertainty. Every shot must have
narration, visual_description and a positive duration_seconds. Maximum 30 shots.
This is a plan for human review, not a renderable scene."""
def validate(plan):
    if not isinstance(plan, dict) or plan.get("schema") != SCHEMA:
        raise ValueError("Invalid scene plan schema")
    shots = plan.get("shots")
    if not isinstance(shots, list) or not 1 <= len(shots) <= 30:
        raise ValueError("Expected 1..30 shots")
    ids = set()
    for shot in shots:
        if not isinstance(shot, dict) or any(not isinstance(shot.get(k), str) or not shot[k].strip()
             for k in ("id", "narration", "visual_description")):
            raise ValueError("Incomplete shot")
        if shot["id"] in ids: raise ValueError("Duplicate shot ID")
        ids.add(shot["id"])
        duration = shot.get("duration_seconds")
        if isinstance(duration, bool) or not isinstance(duration, (int, float)) or not 0 < duration <= 120:
            raise ValueError("Invalid shot duration")
    plan["status"] = "draft_requires_review"
    return plan

def generate(script, *, endpoint, model, key, timeout=120):
    if not key: raise ValueError("Missing API key")
    if not endpoint.startswith("https://") and not endpoint.startswith("http://localhost:") and not endpoint.startswith("http://127.0.0.1:"):
        raise ValueError("Use HTTPS API endpoint (or localhost for local providers)")
    payload = {"model": model, "temperature": 0.2,
               "messages": [{"role": "system", "content": SYSTEM},
                            {"role": "user", "content": script}]}
    request = urllib.request.Request(endpoint, data=json.dumps(payload).encode(),
              headers={"Authorization": "Bearer " + key, "Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=timeout) as response:
        data = json.load(response)
    message = data["choices"][0]["message"]["content"]
    if not isinstance(message, str): raise ValueError("Expected textual JSON completion")
    return validate(json.loads(message))

def main():
    p = argparse.ArgumentParser()
    p.add_argument("--script", required=True)
    p.add_argument("--output", required=True)
    p.add_argument("--endpoint", default=os.environ.get("H2_AI_ENDPOINT", "https://api.openai.com/v1/chat/completions"))
    p.add_argument("--model", default=os.environ.get("H2_AI_MODEL", "gpt-4.1-mini"))
    args = p.parse_args()
    script = Path(args.script).read_text(encoding="utf-8-sig").strip()
    if len(script) < 10: p.error("Script must contain at least 10 characters")
    result = generate(script, endpoint=args.endpoint, model=args.model,
                      key=os.environ.get("H2_AI_API_KEY", ""))
    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Draft scene plan: {out} ({len(result['shots'])} shots)")

if __name__ == "__main__":
    main()
