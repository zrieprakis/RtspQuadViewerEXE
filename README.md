# RTSP Quad Viewer

A native Windows app that shows four RTSP cameras in a 2×2 grid. No browser, no server: each quadrant plays its stream directly with libVLC inside the app, and closing the window ends the process.

## Use

- **Settings:** right-click anywhere → *Settings…* (or `Ctrl+,`) and enter the full RTSP URI for each quadrant, e.g. `rtsp://user:pass@192.168.1.x:554/stream1`. Leave a field empty to keep that quadrant blank. The dialog opens automatically when no cameras are configured.
- **Zoom:** double-click a quadrant to fill the window with it; double-click again or press `Esc` to return to the grid.
- **Fullscreen:** `F11` (or right-click → *Fullscreen*); `Esc` leaves it.
- Streams reconnect on their own if a camera drops or stalls. Audio is muted.

Settings are stored in `%APPDATA%\RtspQuadViewer\config.json`.

## Build

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet run --project src\RtspQuadViewer                 # debug run
dotnet publish src\RtspQuadViewer -c Release -o dist    # dist\RtspQuadViewer.exe
```

The release build is one self-contained `RtspQuadViewer.exe` (~100 MB, mostly libVLC) that runs on 64-bit Windows without .NET installed. On first launch it unpacks libVLC to `%TEMP%\.net\RtspQuadViewer\`.

## Notes

- RTSP is forced over TCP. The app opens no listening ports, so if Windows Firewall asks about it, **Cancel** is fine.
- Video is played by libVLC, so H.264 and H.265 cameras both work, with hardware decoding when available.
