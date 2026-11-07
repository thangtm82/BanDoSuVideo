"""Multi-shot render orchestration for reviewed V6.4 plans.

Uses a V6.2-compatible scene template for each shot. This is a functional
blockout renderer, NOT a historical scene compiler; visual descriptions are
not yet translated into historically accurate geometry.
"""
import argparse
import json
import subprocess
import sys
from pathlib import Path

import v6_4_pipeline as pipeline
from v6_4_audio_timeline import compile_timing


def command(args, dry_run=False):
    pipeline.command(args, dry_run=dry_run)


def run(plan_path, template_path, output, *, audio_dir=None, fps=24,
        blender="blender", ffmpeg="ffmpeg", ffprobe="ffprobe",
        project_minutes=None, dry_run=False, preset="bright_final"):
    output = Path(output).resolve()
    plan = json.loads(Path(plan_path).read_text(encoding="utf-8-sig"))
    template = json.loads(Path(template_path).read_text(encoding="utf-8-sig"))
    if template.get("schema") != "h2.historical_world.preview.v6.2":
        raise ValueError("Template must be a V6.2 renderable scene")
    timeline = compile_timing(plan, fps=fps, audio_dir=audio_dir,
        ffprobe=ffprobe, project_minutes=project_minutes)
    output.mkdir(parents=True, exist_ok=True)
    (output / "v6_4_timeline.json").write_text(
        json.dumps(timeline, indent=2, ensure_ascii=False), encoding="utf-8")
    clips = []
    for index, shot in enumerate(timeline["shots"], 1):
        shot_dir = output / ("shot_%03d" % index)
        shot_dir.mkdir(parents=True, exist_ok=True)
        scene_path = shot_dir / "scene.json"
        # Blockout: retain valid geometry and attach review metadata.
        scene = json.loads(json.dumps(template))
        scene["shot_metadata"] = {
            "id": shot["id"], "narration": shot["narration"],
            "visual_description": plan["shots"][index - 1]["visual_description"],
            "render_note": "Template blockout; visual description not compiled"}
        scene_path.write_text(json.dumps(scene, ensure_ascii=False, indent=2),
                              encoding="utf-8")
        if shot["frame_count"] < 2:
            raise ValueError("Shot too short for camera renderer: " + shot["id"])
        pipeline.emit("shot", "started", shot=shot["id"], index=index,
                      total=len(timeline["shots"]))
        # A separate output directory per shot prevents frame collisions.
        silent = pipeline.run(argparse.Namespace(
            scene=str(scene_path), output=str(shot_dir), blender=blender,
            ffmpeg=ffmpeg, frames=shot["frame_count"], fps=fps,
            threads=4, preset=preset, dry_run=dry_run,
            clean_frames=False))["mp4"]
        clip = Path(silent)
        if shot["audio_file"]:
            clip = shot_dir / "with_audio.mp4"
            command([ffmpeg, "-y", "-i", silent, "-i", shot["audio_file"],
                     "-filter_complex", "[1:a]apad[a]",
                     "-map", "0:v:0", "-map", "[a]",
                     "-c:v", "copy", "-c:a", "aac", "-t",
                     str(shot["duration_seconds"]), str(clip)], dry_run=dry_run)
        else:
            clip = shot_dir / "with_silence.mp4"
            command([ffmpeg, "-y", "-i", silent, "-f", "lavfi",
                     "-i", "anullsrc=channel_layout=stereo:sample_rate=48000",
                     "-map", "0:v:0", "-map", "1:a:0",
                     "-c:v", "copy", "-c:a", "aac", "-t",
                     str(shot["duration_seconds"]), str(clip)], dry_run=dry_run)
        if not dry_run:
            pipeline.require_file(clip, "Shot clip")
        clips.append(clip)
        pipeline.emit("shot", "completed", shot=shot["id"])
    # Concat demuxer: paths are normalized and escaped for FFmpeg.
    concat = output / "clips.ffconcat"
    concat.write_text("ffconcat version 1.0\n" + "".join(
        "file '" + str(p).replace("\\", "/").replace("'", "'\\''") + "'\n"
        for p in clips), encoding="utf-8")
    final = output / "v6_4_project.mp4"
    command([ffmpeg, "-y", "-safe", "0", "-f", "concat", "-i", concat,
             "-c", "copy", str(final)], dry_run=dry_run)
    if not dry_run:
        pipeline.require_file(final, "Project MP4")
    pipeline.emit("project", "completed", mp4=str(final),
                  frames=timeline["total_frames"], shots=len(clips))
    return {"mp4": str(final), "timeline": timeline}


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--plan", required=True)
    p.add_argument("--template-scene", required=True)
    p.add_argument("--output", required=True)
    p.add_argument("--audio-dir")
    p.add_argument("--fps", type=int, default=24)
    p.add_argument("--minutes", type=float)
    p.add_argument("--blender", default="blender")
    p.add_argument("--ffmpeg", default="ffmpeg")
    p.add_argument("--ffprobe", default="ffprobe")
    p.add_argument("--preset", default="bright_final")
    p.add_argument("--dry-run", action="store_true")
    a = p.parse_args()
    run(a.plan, a.template_scene, a.output, audio_dir=a.audio_dir,
        fps=a.fps, blender=a.blender, ffmpeg=a.ffmpeg, ffprobe=a.ffprobe,
        project_minutes=a.minutes, dry_run=a.dry_run, preset=a.preset)


if __name__ == "__main__":
    main()
