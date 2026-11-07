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

## V6.4-B: Windows render application (experimental)

A runnable WinForms .NET 9 shell is included at
`engines/h2-historical-world/integration/H2HistoricalWorld.RenderApp`.
It calls the same Python CLI as the CMD workflow and supports stage
progress, logs, cancellation and opening the MP4.

From `engines\h2-historical-world`:

```cmd
dotnet run --project integration\H2HistoricalWorld.RenderApp\H2HistoricalWorld.RenderApp.csproj
```

Set the Scene JSON to an existing V6.2 scene (typically
`build\v6_2_scene.json`), select Python and Blender, then choose
Render MP4. Use a new output directory for each render. Cancel
terminates the Python process tree. The progress UI currently reports
stages rather than a per-frame percentage.

The WinForms shell is a **standalone integration harness**, not yet
wired into the main MAUI application. It requires Windows and the .NET 9
desktop SDK. No binaries or third-party assets are committed.

### Remaining release gates

1. `python -m unittest discover -s tests -p "test_v6_*.py" -v`
2. `dotnet build integration\H2HistoricalWorld.RenderApp\H2HistoricalWorld.RenderApp.csproj`
3. Render 48 frames from the UI; confirm MP4 and manifest.
4. Cancel a render in progress and verify no orphaned Blender process.
5. Render 240 frames; compare Three.js and Blender scenes.
6. Wire the service into the actual H2 Studio MAUI screen and validate
   on Windows.
7. Add AI script-to-scene conversion with a validated schema.

These checks have not been run by the GitHub connector. Keep the PR in
Draft until the remaining work is completed and validated.
