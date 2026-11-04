# Architecture

```
Desktop/WPF UI
  -> BanDoSu.Core (settings, JSON project IO, shot editing, CLI commands)
      -> Node render.mjs / Python prep_voice.py / draft_shots.py / mix.py
  -> WebView2
      -> integration/preview-host.mjs (local loopback HTTP)
          -> original player.html / engine/player.js / packs/bando
```

## Compatibility contracts

- Do not rewrite map math or render engine.
- Read/save `projects/<slug>/story/shots.json` as JSON tree; preserve unknown fields.
- Launch subprocesses using `ProcessStartInfo.ArgumentList` (no shell injection).
- Cancellation kills subprocess trees.
- Project paths must remain inside `<studio>/projects`.
- Save scene files atomically, with a simple `.bak` backup.
- All generated render outputs stay inside the H2 project folders.

## Engineering backlog

- Property-specific scene form controls with round-trip-compatible schema validation.
- Undo/redo for individual scene edits, drag-and-drop timeline tracks and waveform rendering.
- Frame-accurate video/audio playback and live autoreload.
- Add signed Windows installer and automated Windows GitHub Actions build.
- Component tests for JSON IO/shot timing and smoke tests for existing sample projects.
- Support TTS connectors as optional plugins, not a required dependency.
