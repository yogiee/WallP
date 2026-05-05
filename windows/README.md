# WallP — Windows

System-tray app that rotates your desktop wallpaper from your [Wallhaven](https://wallhaven.cc) collections. Windows port of the macOS WallP, with feature parity (minus the Mac-only Liquid Glass and Focus Filter integrations) plus a few Windows-specific niceties.

## Requirements

- Windows 10 2004+ or Windows 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- A [Wallhaven](https://wallhaven.cc) account with an API key

## Features

**Rotation**
- Fetches wallpapers from your Wallhaven collections and caches them locally
- Automatically rotates on a configurable schedule (5 min – 4 hr)
- Multi-monitor: each display gets a unique random image (or repeats when the cache is smaller than the monitor count)
- Multiple collections with a default-collection picker; switch active collection in one click

**Image pipeline**
- Optimizes images with ImageSharp at sync time: Lanczos3 downscale to your largest screen dimension and a center-crop to your aspect ratio so narrow images fill the screen instead of getting pillar-boxed
- JPEG (q=90) or WebP (q=85) — pick in the Cache tab. WebP delivers ~25% smaller files at comparable visual quality
- Optional Gaussian blur applied at display time (cache stays sharp; live preview as you drag the slider) — useful for HTPC / gaming setups where the desktop is rarely visible

**Auto-pause**
- When a fullscreen app is running (games, video, presentations)
- When you're on battery power
- When the session is locked or Windows suspends; auto-resume on unlock / wake
- Skips syncing on metered networks (cellular hotspots, capped data plans)

**Auto-update**
- NetSparkle-based, with ed25519 signature verification of every release
- Pick **Auto-update**, **Ask before installing**, or **Disabled** in the About tab; manual **Check now** button too

**System integration**
- Launch at login (Windows Run-key based, no helper exe)
- Toast notification on sync complete (toggleable in Settings)
- Cache at `%LOCALAPPDATA%\WallP\cache\`, settings at `%APPDATA%\WallP\settings.json`
- Per-monitor DPI awareness (PerMonitorV2)

## Installation

1. Download `WallP-X.Y.Z.zip` from the [Releases](../../releases) page (look for tags prefixed `windows-v…`)
2. Extract anywhere — it's portable, no installer
3. Run `WallP\WallP.exe`. The app appears in your system tray (look in the chevron `^` overflow on first run; you can drag it out to the always-visible area)

Future updates install themselves once you've authorized them in the prompt.

## Setup

1. **Left-click** the tray icon to open the popover (or right-click → Settings…)
2. In Settings → **General**:
   - Paste your Wallhaven **API key** ([get one here](https://wallhaven.cc/settings/account)) and click **Validate** — green check confirms it works
   - Enter your Wallhaven **username** (used to discover your collections)
3. Switch to **Collections** → **Fetch my collections** → pick one from the dropdown → **Add**
4. The app starts syncing in the background. You'll see the wallpaper change as soon as the first image is cached, then the rotator takes over on your configured schedule

## Usage

### Tray icon

- **Left-click**: opens the popover (status pill, collection picker, Shuffle / Pause / Sync, Settings, Quit)
- **Right-click**: same actions as a Windows-native context menu

### Popover

| Control | Action |
|---|---|
| Status pill | Shows Running / Paused / Syncing / Idle at a glance |
| Collection card | Click to switch the active collection (when you have more than one) |
| Shuffle | Pick a new random wallpaper immediately |
| Pause / Resume | Toggle the rotation timer (header text flips with state) |
| Sync now | Manually sync latest wallpapers from Wallhaven |
| Settings… | Open the full settings window |
| Quit | Exit WallP |

### Settings tabs

| Tab | Options |
|---|---|
| **General** | API key + validation, username, launch at login, pause conditions (fullscreen / battery), metered-network respect, sync-complete toast toggle |
| **Collections** | List your collections with default-collection badge, set default, remove (with cache cleanup), discover from Wallhaven |
| **Timing** | Rotation interval (5m / 15m / 30m / 1h / 2h / 4h), display order (random / name / date), sync interval, default collection picker, Sync now with progress |
| **Cache** | Optimize toggle, image format (JPEG / WebP), Gaussian blur slider (0–50 px), max images per collection, total cache size, Clear all cache |
| **About** | App icon (clickable → GitHub), version, update mode (Auto / Ask / Disabled), Check now |

Settings that affect the active wallpaper apply immediately:
- Switching the default collection applies a fresh wallpaper from the new collection
- Changing display order re-orders and shows the next image in the new order
- Dragging the blur slider re-applies the current wallpaper with the new blur (debounced 300 ms)

## Diagnostics

- `%LOCALAPPDATA%\WallP\crash.log` — unhandled-exception traces from `AppDomain`, the WPF dispatcher, and Task scheduler
- `%LOCALAPPDATA%\WallP\netsparkle.log` — auto-update internals (appcast fetch, signature verification, etc.)

If something misbehaves, those two files are the first things to check.

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Optional: Visual Studio 2026 with the ".NET desktop development" workload, or VS Code with the C# Dev Kit extension.

Run the build script from the repo root:

```powershell
# Release: produces a portable WallP\WallP.exe + WallP-X.Y.Z.zip in windows\build\
.\windows\scripts\build-app.ps1

# Debug: app only, faster
.\windows\scripts\build-app.ps1 Debug
```

For the auto-update workflow (signing the release zip + the appcast):

```powershell
# Sign the appcast after editing windows\WallP\WallP.csproj's <Version> and
# adding a new <item> to appcast-windows.xml at the repo root.
.\windows\scripts\sign-appcast.ps1
```

The build script outputs the `sparkle:signature` and `length` values to paste into the new `<item>` block; `sign-appcast.ps1` then writes `appcast-windows.xml.signature` next to the .xml so NetSparkle's strict-mode verification passes.

## Tech stack

- **WPF** + **.NET 10** (`net10.0-windows10.0.19041.0` for WinRT projection)
- **WPF-UI** 4.3 for Fluent / Mica styling
- **H.NotifyIcon.Wpf** for the system-tray icon
- **`IDesktopWallpaper`** (COM) for per-monitor wallpaper application
- **ImageSharp** for image optimization
- **NetSparkle** 3.x for auto-updates with ed25519 signatures
- **Microsoft.Toolkit.Uwp.Notifications** for sync-complete toasts
- **Microsoft.Extensions.Hosting** for DI + lifecycle

## License

MIT
