#!/bin/bash
# Builds dist/BlankSlate.app for macOS.
#
# Usage:  scripts/package-macos.sh [arm64|x64]      (default: arm64)
#
# Produces a self-contained bundle (no .NET install required to run) and
# ad-hoc signs it. For distribution to other Macs, re-sign with a
# Developer ID certificate and notarize:
#   codesign --deep --force --options runtime --sign "Developer ID Application: …" dist/BlankSlate.app
#   xcrun notarytool submit … && xcrun stapler staple dist/BlankSlate.app
set -euo pipefail

ARCH="${1:-arm64}"
RID="osx-$ARCH"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/src/BlankSlate/BlankSlate.csproj"
DIST="$ROOT/dist"
APP="$DIST/BlankSlate.app"
PUBLISH_DIR="$ROOT/src/BlankSlate/bin/Release/net10.0/$RID/publish"

echo "==> Publishing ($RID, self-contained)"
dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained true \
    -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false

echo "==> Assembling $APP"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUBLISH_DIR/." "$APP/Contents/MacOS/"
cp "$ROOT/packaging/Info.plist" "$APP/Contents/"
cp "$ROOT/packaging/BlankSlate.icns" "$APP/Contents/Resources/"

echo "==> Ad-hoc code signing"
codesign --deep --force --sign - "$APP"

echo "==> Done: $APP"
du -sh "$APP"
