# BanDoSu Studio Desktop

Windows WPF application to manage projects, scripts, narration, map shots, previews and video exports using the existing H2 Bản Đồ Sử engine.

> The original H2 engine is an external, locally installed dependency. Configure its root folder (the directory containing `render.mjs`) from the application's Settings tab. The application does not reimplement its Canvas/Chromium/FFmpeg renderer.

## Requirements

- Windows 10/11 x64
- .NET 10 SDK / Visual Studio with WPF Desktop development
- Existing `h2-2d-studio` installation with Node.js, Python, Chromium and FFmpeg

## Development

```powershell
dotnet restore
dotnet build BanDoSu.slnx
```

See `docs/INSTALL-WINDOWS.md` for setup and smoke tests. The solution targets Windows and cannot be built in Linux without the Windows targeting packs.
