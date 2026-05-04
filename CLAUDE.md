# WallP — Claude Code Instructions

This repo contains both the macOS app (`mac/`) and the upcoming Windows app (`windows/`).
Shared at the repo root: `README.md`, `docs/`, `appcast.xml` (Mac/Sparkle), `appcast-windows.xml` (when added).

## Tools to Use

- **Swift LSP** — use LSP tools for Swift code navigation, symbol lookup, and diagnostics when working in `mac/`.
- **Context7** — use the Context7 MCP plugin to look up up-to-date documentation for Swift/SwiftUI/AppKit (Mac) and .NET/WPF/Win32 (Windows) APIs before implementing features or fixing bugs.

## Git / GitHub Workflow

**Never commit, push, create PRs, or publish GitHub releases autonomously.**

Always stop and ask the user before:
- `git commit`
- `git push`
- `gh pr create` / `gh pr merge`
- `gh release create`
- Any version bump in `mac/WallP.xcodeproj/project.pbxproj` or the Windows `.csproj`

The expected flow for any change is:
1. Implement the change locally
2. Build to confirm it compiles (`xcodebuild` for Mac, `dotnet build` for Windows)
3. **Tell the user what's ready and wait for explicit approval** before touching git or GitHub

This ensures the user can test locally before anything is published.

## Build (macOS)

Always use the build script — never call `xcodebuild` with `-derivedDataPath build` directly:

```bash
# Release build (produces mac/build/WallP.app, mac/build/WallP-X.Y.dmg, mac/build/WallP-X.Y.zip)
./mac/scripts/build-app.sh

# Debug build (produces mac/build/WallP.app only, faster)
./mac/scripts/build-app.sh debug
```

The script puts Xcode's intermediate files in `mac/build/.derived/` (gitignored) and outputs only the final artifacts to `mac/build/`:
- `mac/build/WallP.app` — open directly for local testing
- `mac/build/WallP-X.Y.dmg` — installer DMG for release
- `mac/build/WallP-X.Y.zip` — zip archive for release

## Build (Windows)

TBD — Windows port is in `windows/` (WPF + .NET 8). Build instructions will live here once the project is scaffolded.

## Release Checklist (macOS, when explicitly asked)

1. Bump `MARKETING_VERSION` and `CURRENT_PROJECT_VERSION` in `mac/WallP.xcodeproj/project.pbxproj`
2. Run `./mac/scripts/build-app.sh` and confirm it succeeds
3. **Tell the user** — wait for them to test locally before proceeding
4. `git commit` → `git push` → `gh pr create` → `gh pr merge`
5. `gh release create vX.Y.Z mac/build/WallP-X.Y.dmg`
6. Update memory with new version number
