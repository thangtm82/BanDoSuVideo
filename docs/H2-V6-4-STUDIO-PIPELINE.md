# H2 V6.4 — Studio Pipeline (Phase 1)

This branch introduces a **single CLI entry point** that can be invoked by H2
Studio's C# process runner. It builds a V6.3 architectural scene, adds natural
world details, plans a camera path, renders Blender frames, and assembles H.264 MP4.

## Windows usage

Run in `engines\h2-historical-world` after downloading this PR branch:

```cmd
python tools\v6_4_pipeline.py --scene build\v6_2_scene.json --output build\v6_4_run_001 --blender "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --frames 48 --fps 24 --dry-run
python -m unittest discover -s tests -p "test_v6_4_*.py" -v
python tools\v6_4_pipeline.py --scene build\v6_2_scene.json --output build\v6_4_run_002 --blender "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --frames 48 --fps 24
```

Output: `v6_4_architecture.json`, `v6_4_natural.json`,
`v6_4_camera.json`, `frames/frame_*.png`, `v6_4_final.mp4`,
`v6_4_manifest.json`. Use a fresh output directory per run.
A failed subprocess stops the pipeline immediately. Frame sequence
must match the camera plan exactly.

## Integration boundary

H2 Studio should spawn `python tools/v6_4_pipeline.py` with a
`ProcessStartInfo.ArgumentList` (not a shell command string), redirect
stdout/stderr, display stage progress, and support cancellation by
terminating the process tree. The CLI currently accepts an existing V6.2
scene JSON; AI script-to-scene generation and UI wiring are **not** yet
implemented.

## QA gates

- Run V6.3 and V6.4 unit tests.
- Compare the same natural scene in Three.js and Blender.
- Render a 48-frame smoke test, then a fresh 240-frame production run.
- Verify MP4 with ffprobe and playback, including camera collisions.
- Confirm failure handling for missing Blender, FFmpeg and missing frames.

This PR is a V6.4 foundation, not a claim that the complete H2 Studio
user-interface integration is finished.
