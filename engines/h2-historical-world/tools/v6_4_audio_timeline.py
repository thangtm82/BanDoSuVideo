"""V6.4 shot timing: audio duration first, narration estimate otherwise."""
import argparse
import json
import math
import re
import subprocess
from pathlib import Path

from v6_4_ai_scene_plan import validate


def probe_duration(path, ffprobe="ffprobe"):
    p = subprocess.run([ffprobe, "-v", "error", "-show_entries",
        "format=duration", "-of", "default=noprint_wrappers=1:nokey=1",
        str(path)], capture_output=True, text=True, check=True)
    duration = float(p.stdout.strip())
    if not math.isfinite(duration) or duration <= 0:
        raise ValueError("Invalid audio duration: " + str(path))
    return duration


def compile_timing(plan, *, fps=24, audio_dir=None, ffprobe="ffprobe",
                   words_per_minute=130, padding_seconds=0.5, project_minutes=None):
    validate(dict(plan))
    if not isinstance(fps, int) or fps < 1 or fps > 120:
        raise ValueError("FPS must be 1..120")
    if words_per_minute <= 0 or not math.isfinite(words_per_minute):
        raise ValueError("Invalid reading speed")
    if padding_seconds < 0 or not math.isfinite(padding_seconds):
        raise ValueError("Invalid padding")
    if project_minutes is not None and (project_minutes <= 0 or not math.isfinite(project_minutes)):
        raise ValueError("Invalid project minutes")
    cursor = 0
    shots = []
    for shot in plan["shots"]:
        audio = None
        if audio_dir:
            files = [Path(audio_dir) / (shot["id"] + ext)
                     for ext in (".wav", ".mp3", ".m4a", ".flac")]
            matches = [p for p in files if p.is_file()]
            if len(matches) > 1:
                raise ValueError("Multiple audio files for " + shot["id"])
            audio = matches[0] if matches else None
        if audio:
            seconds = probe_duration(audio, ffprobe)
            source = "audio"
        else:
            words = len(re.findall(r"\S+", shot["narration"].strip()))
            seconds = max(4, words * 60 / words_per_minute)
            source = "text_estimate"
        count = max(1, math.ceil((seconds + padding_seconds) * fps))
        shots.append({"id": shot["id"], "timing_source": source,
                      "audio_file": str(audio) if audio else None,
                      "narration": shot["narration"],
                      "start_frame": cursor, "frame_count": count,
                      "end_frame_exclusive": cursor + count,
                      "duration_seconds": round(count / fps, 6),
                      "render_status": "uncompiled_scene"})
        cursor += count
    target = round(project_minutes * 60 * fps) if project_minutes is not None else None
    return {"schema": "h2.historical_world.timeline.v6.4", "fps": fps,
            "timing_mode": "audio_first_text_fallback",
            "total_frames": cursor, "total_seconds": round(cursor / fps, 6),
            "target_project_frames": target,
            "difference_from_project_frames": cursor - target if target is not None else None,
            "render_ready": False, "shots": shots}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--audio-dir")
    parser.add_argument("--ffprobe", default="ffprobe")
    parser.add_argument("--fps", type=int, default=24)
    parser.add_argument("--minutes", type=float)
    parser.add_argument("--words-per-minute", type=float, default=130)
    parser.add_argument("--padding-seconds", type=float, default=0.5)
    args = parser.parse_args()
    plan = json.loads(Path(args.plan).read_text(encoding="utf-8-sig"))
    result = compile_timing(plan, fps=args.fps, audio_dir=args.audio_dir,
        ffprobe=args.ffprobe, words_per_minute=args.words_per_minute,
        padding_seconds=args.padding_seconds, project_minutes=args.minutes)
    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Timeline: {len(result['shots'])} shots, {result['total_frames']} frames; render_ready=false")


if __name__ == "__main__":
    main()
