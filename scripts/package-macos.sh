#!/bin/bash
# Builds dist/BlankSlate.app (and dist/BlankSlate.app.zip for release upload).
#
# Usage:  scripts/package-macos.sh [arm64|x64]      (default: arm64)
#
# The bundle is self-contained, so users do not need .NET installed.
#
# Signing:
#   By default the app is ad-hoc signed, which is enough to run locally but makes
#   macOS show a Gatekeeper warning on other machines (see README "First launch").
#   Once you have an Apple Developer account, set these and re-run to produce a
#   notarized build that opens with no warning:
#
#     export BLANKSLATE_SIGN_ID="Developer ID Application: Your Name (TEAMID)"
#     export BLANKSLATE_NOTARY_PROFILE="notarytool-profile"
#
#   Create the notary profile once with:
#     xcrun notarytool store-credentials "notarytool-profile" \
#         --apple-id you@example.com --team-id TEAMID --password APP_SPECIFIC_PASSWORD
set -euo pipefail

ARCH="${1:-arm64}"
RID="osx-$ARCH"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/src/BlankSlate/BlankSlate.csproj"
DIST="$ROOT/dist"
APP="$DIST/BlankSlate.app"
PUBLISH_DIR="$ROOT/src/BlankSlate/bin/Release/net10.0/$RID/publish"

# Single source of truth, shared with the .csproj.
VERSION="$(sed -n 's/.*<BlankSlateVersion>\(.*\)<\/BlankSlateVersion>.*/\1/p' "$ROOT/Directory.Build.props")"
[ -n "$VERSION" ] || { echo "Could not read BlankSlateVersion from Directory.Build.props"; exit 1; }
echo "==> BlankSlate $VERSION ($RID)"

echo "==> Publishing (self-contained)"
dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained true \
    -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false

echo "==> Assembling $APP"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUBLISH_DIR/." "$APP/Contents/MacOS/"
cp "$ROOT/packaging/Info.plist" "$APP/Contents/"
cp "$ROOT/packaging/BlankSlate.icns" "$APP/Contents/Resources/"

# Stamp the version so the bundle and the About dialog always agree.
/usr/libexec/PlistBuddy -c "Set :CFBundleVersion $VERSION" "$APP/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $VERSION" "$APP/Contents/Info.plist"

if [ -n "${BLANKSLATE_SIGN_ID:-}" ]; then
    echo "==> Signing with Developer ID (hardened runtime)"
    codesign --deep --force --timestamp --options runtime \
        --sign "$BLANKSLATE_SIGN_ID" "$APP"
else
    echo "==> Ad-hoc signing (no Developer ID set)"
    codesign --deep --force --sign - "$APP"
fi

echo "==> Zipping for release"
rm -f "$DIST/BlankSlate.app.zip"
ditto -c -k --keepParent "$APP" "$DIST/BlankSlate.app.zip"

if [ -n "${BLANKSLATE_NOTARY_PROFILE:-}" ]; then
    echo "==> Notarizing (this can take a few minutes)"
    xcrun notarytool submit "$DIST/BlankSlate.app.zip" \
        --keychain-profile "$BLANKSLATE_NOTARY_PROFILE" --wait
    xcrun stapler staple "$APP"
    # Re-zip so the uploaded archive contains the stapled ticket.
    rm -f "$DIST/BlankSlate.app.zip"
    ditto -c -k --keepParent "$APP" "$DIST/BlankSlate.app.zip"
    echo "==> Notarized and stapled"
fi

echo "==> Done"
du -sh "$APP" "$DIST/BlankSlate.app.zip"
