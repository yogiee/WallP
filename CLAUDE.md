# WallP — Claude Code Instructions

## Tools to Use

- **Swift LSP** — use LSP tools for Swift code navigation, symbol lookup, and diagnostics when working on Swift source files.
- **Context7** — use the Context7 MCP plugin to look up up-to-date documentation for Swift, SwiftUI, AppKit, and Apple frameworks before implementing features or fixing bugs.

## Git / GitHub Workflow

**Never commit, push, create PRs, or publish GitHub releases autonomously.**

Always stop and ask the user before:
- `git commit`
- `git push`
- `gh pr create` / `gh pr merge`
- `gh release create`
- Any version bump in `project.pbxproj`

The expected flow for any change is:
1. Implement the change locally
2. Build (`xcodebuild`) to confirm it compiles
3. **Tell the user what's ready and wait for explicit approval** before touching git or GitHub

This ensures the user can test locally before anything is published.

## Build

Always use the build script — never call `xcodebuild` directly with `-derivedDataPath build`:

```bash
# Release build (produces build/WallP.app, build/WallP-X.Y.dmg, build/WallP-X.Y.zip)
./scripts/build-app.sh

# Debug build (produces build/WallP.app only, faster)
./scripts/build-app.sh debug
```

The script puts Xcode's intermediate files in `build/.derived/` (gitignored) and outputs only the final artifacts to `build/`:
- `build/WallP.app` — open directly for local testing
- `build/WallP-X.Y.dmg` — installer DMG for release
- `build/WallP-X.Y.zip` — zip archive for release

## Release Checklist (when explicitly asked)

1. Bump `MARKETING_VERSION` and `CURRENT_PROJECT_VERSION` in `project.pbxproj`
2. Run `./scripts/build-app.sh` and confirm it succeeds
3. **Tell the user** — wait for them to test locally before proceeding
4. `git commit` → `git push` → `gh pr create` → `gh pr merge`
5. `gh release create vX.Y.Z build/WallP-X.Y.dmg`
6. Update memory with new version number
