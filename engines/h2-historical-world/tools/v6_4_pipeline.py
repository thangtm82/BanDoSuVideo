"""H2 V6.4: one-command, fail-fast scene-to-MP4 pipeline.

Standard-library only. Designed as a stable CLI entry point for H2 Studio.
All paths are explicit; no shell=True and no silent partial success.
"""
import argparse
import datetime
import json
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TOOLS = ROOT / "tools"


def emit(stage, status, **fields):
    event = {"schema": "h2.studio.progress.v6.4", "stage": stage, "status": status,
             "timestamp": datetime.datetime.now(datetime.timezone.utc).isoformat(), **fields}
    print("H2_PROGRESS " + json.dumps(event, ensure_ascii=False), flush=True)


def command(args, *, dry_run=False):
    print("RUN:", subprocess.list2cmdline([str(x) for x in args]), flush=True)
    if not dry_run:
        subprocess.run([str(x) for x in args], check=True)


def require_file(path, label):
    path = Path(path)
    if not path.is_file() or path.stat().st_size == 0:
        raise RuntimeError(f"{label} missing or empty: {path}")
    return path


def build(scene, output_dir, *, python=sys.executable, dry_run=False):
    scene = require_file(scene, "Input V6.2 scene")
    output_dir = Path(output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    upgraded = output_dir / "v6_4_architecture.json"
    natural = output_dir / "v6_4_natural.json"
    command([python, TOOLS / "v6_3_scene_upgrade.py", scene, upgraded], dry_run=dry_run)
    if not dry_run:
        require_file(upgraded, "Architecture scene")
    command([python, TOOLS / "v6_3_natural_world.py", upgraded, natural], dry_run=dry_run)
    if not dry_run:
        require_file(natural, "Natural scene")
        data = json.loads(natural.read_text(encoding="utf-8-sig"))
        if data.get("schema") != "h2.historical_world.preview.v6.2":
            raise ValueError("Unexpected natural scene schema")
    return natural


def plan(natural, output_dir, *, frames=240, fps=24, python=sys.executable, dry_run=False):
    if frames < 2 or fps < 1:
        raise ValueError("frames >= 2 and fps >= 1 required")
    if not dry_run:
        require_file(natural, "Natural scene")
    camera = Path(output_dir).resolve() / "v6_4_camera.json"
    command([python, TOOLS / "v6_3_cinematic.py", "plan", natural, camera,
             "--frames", frames, "--fps", fps], dry_run=dry_run)
    if not dry_run:
        require_file(camera, "Camera plan")
    return camera


def render(natural, camera, output_dir, *, blender, preset="bright_final", threads=4,
           dry_run=False, clean_frames=False):
    if not dry_run:
        require_file(natural, "Natural scene")
        require_file(camera, "Camera plan")
    if threads < 1:
        raise ValueError("threads must be positive")
    folder = Path(output_dir).resolve() / "frames"
    existing = list(folder.glob("frame_*.png")) if folder.exists() else []
    if existing and not clean_frames:
        raise RuntimeError(f"Frames folder not empty: {folder}. Use a fresh output directory.")
    if existing and clean_frames and not dry_run:
        for frame in existing:
            frame.unlink()
    folder.mkdir(parents=True, exist_ok=True)
    command([blender, "-b", "-t", threads, "--python",
             TOOLS / "v6_3_blender_animation.py", "--", natural, camera, folder, preset],
            dry_run=dry_run)
    if not dry_run:
        manifest = json.loads(Path(camera).read_text(encoding="utf-8-sig"))
        expected = ["frame_%05d.png" % i for i in range(1, manifest["frames"] + 1)]
        actual = sorted(p.name for p in folder.glob("frame_*.png"))
        if actual != expected:
            raise RuntimeError(f"Frame sequence mismatch: expected {len(expected)}, got {len(actual)}")
    return folder


def assemble(frames_dir, output_dir, *, fps=24, ffmpeg="ffmpeg",
             python=sys.executable, dry_run=False):
    mp4 = Path(output_dir).resolve() / "v6_4_final.mp4"
    if not dry_run and not Path(ffmpeg).is_file() and not shutil.which(ffmpeg):
        raise FileNotFoundError(f"FFmpeg not found: {ffmpeg}")
    command([python, TOOLS / "v6_3_cinematic.py", "assemble", frames_dir, mp4,
             "--fps", fps, "--ffmpeg", ffmpeg], dry_run=dry_run)
    if not dry_run:
        require_file(mp4, "MP4 output")
    return mp4


def run(args):
    out = Path(args.output).resolve()
    out.mkdir(parents=True, exist_ok=True)
    if not args.dry_run:
        require_file(args.scene, "Input V6.2 scene")
        if not Path(args.blender).is_file() and not shutil.which(args.blender):
            raise FileNotFoundError(f"Blender not found: {args.blender}")
        if not Path(args.ffmpeg).is_file() and not shutil.which(args.ffmpeg):
            raise FileNotFoundError(f"FFmpeg not found: {args.ffmpeg}")
    if not args.clean_frames and (out / "frames").exists() and any((out / "frames").glob("frame_*.png")):
        raise RuntimeError(f"Frames folder not empty: {out / 'frames'}. Use a fresh output directory.")
    emit("scene", "started")
    natural = build(args.scene, out, dry_run=args.dry_run)
    emit("scene", "completed")
    emit("camera", "started")
    camera = plan(natural, out, frames=args.frames, fps=args.fps, dry_run=args.dry_run)
    emit("camera", "completed")
    emit("render", "started", frames=args.frames)
    folder = render(natural, camera, out, blender=args.blender,
                    preset=args.preset, threads=args.threads, dry_run=args.dry_run,
                    clean_frames=args.clean_frames)
    emit("render", "completed", frames=args.frames)
    emit("encode", "started")
    mp4 = assemble(folder, out, fps=args.fps, ffmpeg=args.ffmpeg, dry_run=args.dry_run)
    emit("encode", "completed", mp4=str(mp4))
    manifest = {"schema": "h2.studio.pipeline.v6.4", "status": "planned" if args.dry_run else "completed",
                "scene": str(natural), "camera": str(camera), "frames_dir": str(folder),
                "mp4": str(mp4), "frames": args.frames, "fps": args.fps,
                "preset": args.preset}
    if not args.dry_run:
        (out / "v6_4_manifest.json").write_text(
            json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(manifest, indent=2))
    return manifest


def main():
    parser = argparse.ArgumentParser(description="H2 V6.4: scene to cinematic MP4")
    parser.add_argument("--scene", required=True, help="V6.2 scene JSON input")
    parser.add_argument("--output", required=True, help="Fresh output directory")
    parser.add_argument("--blender", default="blender", help="Blender executable path")
    parser.add_argument("--ffmpeg", default="ffmpeg", help="FFmpeg executable path")
    parser.add_argument("--frames", type=int, default=240)
    parser.add_argument("--fps", type=int, default=24)
    parser.add_argument("--threads", type=int, default=4)
    parser.add_argument("--preset", default="bright_final")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--clean-frames", action="store_true", help="Remove existing frame_*.png in the selected output folder")
    args = parser.parse_args()
    try:
        run(args)
    except (OSError, ValueError, RuntimeError, subprocess.CalledProcessError) as exc:
        parser.exit(1, f"H2 V6.4 failed: {exc}\n")


if __name__ == "__main__":
    main()
