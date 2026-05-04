#!/bin/bash
# Build WallP as a release .app, DMG, and ZIP
# Usage: ./scripts/build-app.sh [release|debug]
#
# Output (build/):
#   WallP.app      — run or drag to /Applications for local testing
#   WallP.dmg      — installer DMG (release only)
#   WallP.zip      — zip archive  (release only)

set -euo pipefail

CONFIG="${1:-release}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
BUILD_DIR="$PROJECT_DIR/build"
DERIVED_DIR="$BUILD_DIR/.derived"
APP_NAME="WallP"

# Read version from project.pbxproj
VERSION=$(grep 'MARKETING_VERSION' "$PROJECT_DIR/WallP.xcodeproj/project.pbxproj" | head -1 | sed 's/.*= //;s/;//;s/ //')
BUILD_NUMBER=$(grep 'CURRENT_PROJECT_VERSION' "$PROJECT_DIR/WallP.xcodeproj/project.pbxproj" | head -1 | sed 's/.*= //;s/;//;s/ //')

mkdir -p "$BUILD_DIR"

echo "=== Building $APP_NAME $VERSION ($CONFIG) ==="

# Step 1: Compile with xcodebuild (derived data goes to build/.derived, not polluting build/)
echo "  [1/4] Compiling..."
if [ "$CONFIG" = "release" ]; then
    XCODE_CONFIG="Release"
else
    XCODE_CONFIG="Debug"
fi

xcodebuild \
    -scheme "$APP_NAME" \
    -configuration "$XCODE_CONFIG" \
    -derivedDataPath "$DERIVED_DIR" \
    clean build \
    2>&1 | grep -E "^(error:|warning:|Build succeeded|BUILD SUCCEEDED|BUILD FAILED|/.*error:|/.*warning:)" || true

BUILT_APP="$DERIVED_DIR/Build/Products/$XCODE_CONFIG/$APP_NAME.app"

if [ ! -d "$BUILT_APP" ]; then
    echo "ERROR: Build failed — app not found at $BUILT_APP"
    exit 1
fi

# Step 2: Copy .app to build/
echo "  [2/4] Copying app bundle..."
rm -rf "$BUILD_DIR/$APP_NAME.app"
cp -R "$BUILT_APP" "$BUILD_DIR/$APP_NAME.app"

if [ "$CONFIG" != "release" ]; then
    echo ""
    echo "=== Build complete ==="
    echo "  App: $BUILD_DIR/$APP_NAME.app"
    echo ""
    echo "To run:     open \"$BUILD_DIR/$APP_NAME.app\""
    echo "To install: cp -R \"$BUILD_DIR/$APP_NAME.app\" /Applications/"
    exit 0
fi

# Step 3: Create ZIP
echo "  [3/4] Creating ZIP..."
ZIP_PATH="$BUILD_DIR/$APP_NAME-$VERSION.zip"
rm -f "$ZIP_PATH"
(cd "$BUILD_DIR" && zip -qr "$APP_NAME-$VERSION.zip" "$APP_NAME.app")
echo "         $(du -sh "$ZIP_PATH" | cut -f1)  $APP_NAME-$VERSION.zip"

# Step 4: Create DMG
echo "  [4/4] Creating DMG..."
DMG_PATH="$BUILD_DIR/$APP_NAME-$VERSION.dmg"
STAGING_DIR="$BUILD_DIR/.dmg_staging"

rm -f "$DMG_PATH"
rm -rf "$STAGING_DIR"
mkdir -p "$STAGING_DIR"

cp -R "$BUILD_DIR/$APP_NAME.app" "$STAGING_DIR/"
ln -s /Applications "$STAGING_DIR/Applications"

hdiutil create \
    -volname "$APP_NAME $VERSION" \
    -srcfolder "$STAGING_DIR" \
    -ov -format UDZO \
    "$DMG_PATH" > /dev/null

rm -rf "$STAGING_DIR"
echo "         $(du -sh "$DMG_PATH" | cut -f1)  $APP_NAME-$VERSION.dmg"

# Sign DMG with EdDSA for Sparkle update verification
SIGN_UPDATE="$DERIVED_DIR/SourcePackages/artifacts/sparkle/Sparkle/bin/sign_update"
if [ -f "$SIGN_UPDATE" ]; then
    echo "  Signing DMG for Sparkle..."
    EDDSA_SIG=$("$SIGN_UPDATE" "$DMG_PATH" 2>/dev/null | grep "sparkle:edSignature" | sed 's/.*sparkle:edSignature="\([^"]*\)".*/\1/')
    DMG_SIZE=$(stat -f%z "$DMG_PATH")
    echo ""
    echo "  *** Update appcast.xml with:"
    echo "      sparkle:version=\"$BUILD_NUMBER\""
    echo "      sparkle:shortVersionString=\"$VERSION\""
    echo "      sparkle:edSignature=\"$EDDSA_SIG\""
    echo "      length=\"$DMG_SIZE\""
else
    echo "  Note: sign_update not found — run the build once to resolve packages first."
fi

echo ""
echo "=== Build complete ==="
echo "  App: $BUILD_DIR/$APP_NAME.app"
echo "  DMG: $DMG_PATH"
echo "  ZIP: $ZIP_PATH"
echo ""
echo "To test:    open \"$BUILD_DIR/$APP_NAME.app\""
echo "To install: open \"$DMG_PATH\""
