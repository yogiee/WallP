# WallP — Windows

Windows port of WallP. Stack:

- **WPF + .NET 8** (C#) for UI, with WPF-UI for Fluent/Mica styling
- **`IDesktopWallpaper`** (COM) for wallpaper setting (per-monitor support)
- **ImageSharp** for image optimization (JPEG default, HEIC if HEIF Image Extension installed)
- **NetSparkle** for auto-update (reads `appcast-windows.xml` from the repo root)

Status: scaffolding in progress.

## Planned features

Parity with macOS, minus Mac-only features (Liquid Glass, Focus Filters), plus Windows-specific:

- System tray app — right-click for menu (Play/Pause/Shuffle/Sync/Settings/Quit), left-click opens Settings
- Wallhaven API key + collections, multi-collection with default picker
- Multi-monitor support (unique random per monitor)
- Local image cache at `%LOCALAPPDATA%\WallP\cache\`
- Settings JSON at `%APPDATA%\WallP\settings.json`
- Pause triggers: fullscreen app running, on battery, on metered network
- Toast notification on sync complete (toggleable)
- Optional Gaussian blur for HTPC/gaming setups
- Launch at login

## Build

TBD — will use a PowerShell build script analogous to `mac/scripts/build-app.sh`.
