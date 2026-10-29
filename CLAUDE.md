# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A native Windows (WinForms, .NET 10) app that plays four RTSP cameras in a 2×2 grid using LibVLCSharp. The design goal is that closing the window **fully terminates** everything, with no child processes and no background services. It replaced an earlier Node + go2rtc browser version (github.com/zrieprakis/Claude_rtsp), which must not be reintroduced here.

## Commands

```powershell
dotnet build src\RtspQuadViewer                         # debug build → bin\Debug\net10.0-windows\win-x64\
dotnet run --project src\RtspQuadViewer
dotnet publish src\RtspQuadViewer -c Release -o dist    # single self-contained exe (~100 MB)
```

There are no tests. Verify by running the app against real cameras (the user's URIs are already in `%APPDATA%\RtspQuadViewer\config.json`; never print or commit them). This machine had no NuGet source configured; `nuget.org` was added at user level.

## Architecture

- `Program.cs`: calls `Core.Initialize` with `AppContext.BaseDirectory\libvlc\win-x64` (this also works for the single-file extraction dir), runs `MainForm`, then calls **`Environment.Exit(0)`**. libvlc keeps native threads alive, and the hard exit is what guarantees the process ends.
- `MainForm.cs`: owns the single shared `LibVLC` (`--rtsp-tcp`) and the four `CameraPanel`s. The layout is manual (`LayoutPanels`), not a TableLayoutPanel, so that zoom can hide three panels and stretch one. Keys go through `ProcessCmdKey`: F11 fullscreen, Ctrl+, settings, Esc unzoom/exit fullscreen. On closing, it waits ≤2 s for the panels to stop.
- `CameraPanel.cs`: one `MediaPlayer` + `VideoView` per quadrant.
  - **Threading rules:** blocking libvlc calls (Play/Stop/Dispose) go through `Enqueue`, a sequential task chain on the thread pool, never on the UI thread. libvlc events fire on libvlc threads and must only marshal to the UI (`OnUi`), never call back into libvlc (that deadlocks).
  - Reconnect is a 2 s UI `Timer` watchdog. It polls `State`/`Time`, restarts on error, end or stop, on a stall (Time unchanged 10 s), or on connect timeout (20 s). It skips a tick while an op is in flight.
  - `EnableMouseInput/EnableKeyInput = false` is what lets double-click and right-click reach WinForms over the video window.
- `AppConfig.cs`: `{ "cams": [4 strings] }` JSON in `%APPDATA%`, the same format as the old web version's `config.json`. Validation is a prefix check only (`rtsp://`/`rtsps://`, no whitespace), because camera URIs often contain characters `System.Uri` rejects.

The csproj disables the x86/arm64 libvlc copies (`VlcWindowsX86Enabled`/`VlcWindowsArm64Enabled`). Release-only properties produce the single-file exe (`IncludeAllContentForSelfExtract` bundles the libvlc plugin folder).

## Gotchas

- Windows Firewall may prompt about the exe (once per exe path). The app opens no listening sockets, so don't add firewall rules; Cancel is fine. Never click the prompt in automated UI tests: it's always on top and intercepts synthetic clicks.
