#!/usr/bin/env bash
# Wraps a published macOS binary into a TerminalHyperspace.app bundle and
# (optionally) signs + notarizes it using your Apple Developer credentials.
#
# Usage:  Scripts/package-macos.sh <rid>
#         where <rid> is osx-arm64 or osx-x64
#
# Required env var for signing (skipped when unset):
#   DEVELOPER_ID                e.g. "Developer ID Application: Jane Doe (ABCDE12345)"
#
# Optional env vars for notarization (set NOTARIZE=1 to enable):
#   NOTARIZE=1                  set to a non-empty value to submit + staple
#   NOTARY_PROFILE              keychain profile name (preferred)
#   APPLE_ID, APPLE_TEAM_ID, APPLE_APP_PASSWORD
#                               raw credentials (used only when NOTARY_PROFILE unset)
#
# Recommended one-time keychain setup so this script doesn't need raw passwords:
#   xcrun notarytool store-credentials TerminalHyperspace-Notary \
#       --apple-id   "$APPLE_ID" \
#       --team-id    "$APPLE_TEAM_ID" \
#       --password   "$APPLE_APP_PASSWORD"
#   export NOTARY_PROFILE=TerminalHyperspace-Notary
set -euo pipefail

RID="${1:-osx-arm64}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUBLISH_DIR="$ROOT/bin/Release/net10.0/$RID/publish"
BIN="$PUBLISH_DIR/TerminalHyperspace"
ICON="$ROOT/Assets/logo.icns"
ENTITLEMENTS="$ROOT/Scripts/macos-entitlements.plist"

if [[ ! -f "$BIN" ]]; then
  echo "error: $BIN not found — run 'dotnet publish -c Release -r $RID --self-contained' first" >&2
  exit 1
fi
if [[ ! -f "$ICON" ]]; then
  echo "error: $ICON not found" >&2
  exit 1
fi

APP="$PUBLISH_DIR/TerminalHyperspace.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

cp "$BIN" "$APP/Contents/MacOS/TerminalHyperspace"
chmod +x "$APP/Contents/MacOS/TerminalHyperspace"
cp "$ICON" "$APP/Contents/Resources/logo.icns"

cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>            <string>Terminal Hyperspace</string>
    <key>CFBundleDisplayName</key>     <string>Terminal Hyperspace</string>
    <key>CFBundleIdentifier</key>      <string>com.openhyperspace.terminalhyperspace</string>
    <key>CFBundleVersion</key>         <string>1.0.0</string>
    <key>CFBundleShortVersionString</key><string>1.0.0</string>
    <key>CFBundleExecutable</key>      <string>TerminalHyperspace</string>
    <key>CFBundleIconFile</key>        <string>logo</string>
    <key>CFBundlePackageType</key>     <string>APPL</string>
    <key>LSMinimumSystemVersion</key>  <string>11.0</string>
    <key>NSHighResolutionCapable</key> <true/>
    <key>NSPrincipalClass</key>        <string>NSApplication</string>
</dict>
</plist>
PLIST

echo "Built: $APP"

# ---- Code signing ----
if [[ -n "${DEVELOPER_ID:-}" ]]; then
  if [[ ! -f "$ENTITLEMENTS" ]]; then
    echo "error: $ENTITLEMENTS not found" >&2
    exit 1
  fi
  echo "Signing with: $DEVELOPER_ID"
  # --deep walks every embedded mach-o (the single-file binary unpacks at runtime,
  # but the bundled native loaders we ship still need signatures).
  # --options runtime enables the hardened runtime; the entitlements relax it
  # just enough that the .NET JIT can run.
  codesign --force --deep --options runtime --timestamp \
           --entitlements "$ENTITLEMENTS" \
           --sign "$DEVELOPER_ID" \
           "$APP"
  echo "Verifying signature..."
  codesign --verify --deep --strict --verbose=2 "$APP"
  spctl --assess --type execute --verbose "$APP" || \
    echo "  (note: spctl may report 'rejected' until the app is notarized)"
else
  echo "skipping signing — DEVELOPER_ID not set"
  echo "  export DEVELOPER_ID=\"Developer ID Application: <Your Name> (TEAMID)\""
  echo "  to sign. Run 'security find-identity -v -p codesigning' to list identities."
fi

# ---- Notarization (optional) ----
if [[ -n "${NOTARIZE:-}" && -n "${DEVELOPER_ID:-}" ]]; then
  ZIP="$PUBLISH_DIR/TerminalHyperspace-$RID.zip"
  echo "Zipping for notarization → $ZIP"
  ditto -c -k --keepParent "$APP" "$ZIP"

  echo "Submitting to notary service..."
  if [[ -n "${NOTARY_PROFILE:-}" ]]; then
    xcrun notarytool submit "$ZIP" --keychain-profile "$NOTARY_PROFILE" --wait
  else
    : "${APPLE_ID:?APPLE_ID is required when NOTARIZE=1 without NOTARY_PROFILE}"
    : "${APPLE_TEAM_ID:?APPLE_TEAM_ID is required when NOTARIZE=1 without NOTARY_PROFILE}"
    : "${APPLE_APP_PASSWORD:?APPLE_APP_PASSWORD is required when NOTARIZE=1 without NOTARY_PROFILE}"
    xcrun notarytool submit "$ZIP" \
      --apple-id "$APPLE_ID" \
      --team-id "$APPLE_TEAM_ID" \
      --password "$APPLE_APP_PASSWORD" \
      --wait
  fi

  echo "Stapling notarization ticket..."
  xcrun stapler staple "$APP"
  xcrun stapler validate "$APP"
fi
