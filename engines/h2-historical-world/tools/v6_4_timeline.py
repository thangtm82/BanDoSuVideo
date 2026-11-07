"""Deterministic, audio-independent V6.4 scene-plan timing compiler.

Produces an approved-review timeline manifest; it does NOT claim that a textual
shot can be rendered by the legacy single-scene V6.2 renderer.
"""
import argparse
import json
from decimal import Decimal, ROUND_FLOOR
from pathlib import Path

from v6_4_ai_scene_plan import validate


def compile_timeline(plan, *, minutes, fps=24):
    validate(dict(plan))
    if not isinstance(minutes, (int, float)) or isinstance(minutes, bool) or minutes <= 0:
        raise ValueError("Project minutes must be positive")
    if not isinstance(fps, int) or isinstance(fps, bool) or fps < 1 or fps > 120:
        raise ValueError("FPS must be an integer from 1 to 120")
    total = int((Decimal(str(minutes)) * Decimal(60) * fps).to_integral_value())
    shots = plan["shots"]
    if total < len(shots):
        raise ValueError("Duration too short to allocate one frame per shot")
    weights = [Decimal(str(s["duration_seconds"])) for s in shots]
    weight_sum = sum(weights)
    remaining = total - len(shots)
    quotas = [remaining * w / weight_sum for w in weights]
    counts = [1 + int(q.to_integral_value(rounding=ROUND_FLOOR)) for q in quotas]
    missing = total - sum(counts)
    order = sorted(range(len(shots)), key=lambda i: (-(quotas[i] - int(quotas[i])), i))
    for i in order[:missing]:
        counts[i] += 1
    cursor = 0
    items = []
    for shot, count in zip(shots, counts):
        items.append({
            "id": shot["id"], "start_frame": cursor, "frame_count": count,
            "end_frame_exclusive": cursor + count,
            "start_seconds": round(cursor / fps, 6),
            "duration_seconds": round(count / fps, 6),
            "visual_description": shot["visual_description"],
            "narration": shot["narration"],
            "render_status": "uncompiled_scene"
        })
        cursor += count
    return {
        "schema": "h2.historical_world.timeline.v6.4",
        "source_schema": plan["schema"], "source_status": plan.get("status", "draft_requires_review"),
        "fps": fps, "project_minutes": minutes, "total_frames": total,
        "total_seconds": total / fps, "audio_required": False,
        "render_ready": False, "shots": items
    }


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--plan", required=True)
    p.add_argument("--minutes", type=float, required=True)
    p.add_argument("--fps", type=int, default=24)
    p.add_argument("--output", required=True)
    a = p.parse_args()
    plan = json.loads(Path(a.plan).read_text(encoding="utf-8-sig"))
    timeline = compile_timeline(plan, minutes=a.minutes, fps=a.fps)
    out = Path(a.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(timeline, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Timeline: {len(timeline['shots'])} shots, {timeline['total_frames']} frames, "
          f"{timeline['total_seconds']}s. NOT RENDER READY.")


if __name__ == "__main__":
    main()
