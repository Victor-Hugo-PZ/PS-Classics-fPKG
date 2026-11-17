#!/usr/bin/env bash
# Builds the Linux or macOS release of PS Classics fPKG Builder: the app, the PkgCheck tool and the Tools folder,
# self-contained (no .NET install needed).
#
#   scripts/build-release.sh <version> <rid>
#     rid: linux-x64 | osx-arm64 | osx-x64
#
# Output (in dist/):
#   linux-x64  -> PS.Classics.fPKG.Builder.v<version>.Linux-x64.tar.gz   (folder with the app, PkgCheck and Tools)
#   osx-*      -> PS.Classics.fPKG.Builder.v<version>.macOS-<arch>.zip  ("PS Classics fPKG Builder.app", ad-hoc signed)
#
# Requires the .NET 10 SDK and the git-ignored Tools folder in the repository root (see README).
# The macOS package must be built on a Mac (codesign, iconutil, ditto).
set -euo pipefail

version="${1:?usage: build-release.sh <version> <rid>}"
rid="${2:?usage: build-release.sh <version> <rid>}"
repo="$(cd "$(dirname "$0")/.." && pwd)"
dist="$repo/dist"
app_project="$repo/CrossPlatform/PSClassicsfPKGBuilder/PSClassicsfPKGBuilder.csproj"
pkgcheck_project="$repo/Windows/PkgCheck/PkgCheck.vbproj"
name="PS Classics fPKG Builder"

[ -f "$repo/Tools/PS4/gengp4_patch.exe" ] || { echo "The Tools folder was not found in $repo (see README)." >&2; exit 1; }
[ -f "$repo/external/LibOrbisPkg/LibOrbisPkg/LibOrbisPkg.csproj" ] || { echo "Run: git submodule update --init" >&2; exit 1; }

publish() { # <project> <output dir> [extra args]
  local project="$1" out="$2"; shift 2
  dotnet publish "$project" -c Release -r "$rid" --self-contained true -o "$out" "-p:Version=$version" --nologo -v q "$@"
}

mkdir -p "$dist"

case "$rid" in
  linux-*)
    stage="$dist/$name v$version"
    archive="$dist/PS.Classics.fPKG.Builder.v$version.Linux-${rid#linux-}.tar.gz"
    rm -rf "$stage" "$archive"
    echo "Publishing the app for $rid..."
    publish "$app_project" "$stage" -p:CopyToolsToOutput=true
    echo "Publishing PkgCheck..."
    publish "$pkgcheck_project" "$stage"
    cp "$repo/scripts/release-readme-linux.txt" "$stage/README.txt"
    cp "$repo/CrossPlatform/PSClassicsfPKGBuilder/Assets/app.png" "$stage/app.png" 2>/dev/null || true
    cp "$repo/scripts/linux-install-shortcut.sh" "$stage/install-shortcut.sh"
    chmod +x "$stage/PSClassicsfPKGBuilder" "$stage/PkgCheck" "$stage/install-shortcut.sh"
    tar -C "$dist" -czf "$archive" "$(basename "$stage")"
    ;;

  osx-*)
    bundle_root="$dist/macos-$rid"
    bundle="$bundle_root/$name.app"
    archive="$dist/PS.Classics.fPKG.Builder.v$version.macOS-${rid#osx-}.zip"
    rm -rf "$bundle_root" "$archive"
    macos_dir="$bundle/Contents/MacOS"
    resources="$bundle/Contents/Resources"
    mkdir -p "$macos_dir" "$resources"

    echo "Publishing the app for $rid..."
    publish "$app_project" "$macos_dir" -p:CopyToolsToOutput=true
    echo "Publishing PkgCheck..."
    publish "$pkgcheck_project" "$macos_dir"

    sed "s/@VERSION@/$version/g" "$repo/scripts/macos-Info.plist" > "$bundle/Contents/Info.plist"

    # App icon: app.ico -> app.icns (best effort; the app works without it)
    if command -v sips >/dev/null && command -v iconutil >/dev/null; then
      tmp="$(mktemp -d)"
      if sips -s format png "$repo/CrossPlatform/PSClassicsfPKGBuilder/Assets/app.ico" --out "$tmp/icon.png" >/dev/null 2>&1; then
        mkdir -p "$tmp/app.iconset"
        for size in 16 32 128 256 512; do
          sips -z $size $size "$tmp/icon.png" --out "$tmp/app.iconset/icon_${size}x${size}.png" >/dev/null
          sips -z $((size * 2)) $((size * 2)) "$tmp/icon.png" --out "$tmp/app.iconset/icon_${size}x${size}@2x.png" >/dev/null
        done
        iconutil -c icns "$tmp/app.iconset" -o "$resources/app.icns" || true
      fi
      rm -rf "$tmp"
    fi

    # Ad-hoc signature (no Apple developer account): required for Apple Silicon to run the binaries at all.
    # Gatekeeper still warns on the first launch; README.txt explains how to open it.
    codesign --force --deep --sign - "$bundle"

    cp "$repo/scripts/release-readme-macos.txt" "$bundle_root/README.txt"
    (cd "$bundle_root" && ditto -c -k --sequesterRsrc --keepParent "$name.app" "$archive" && zip -q "$archive" README.txt)
    ;;

  *)
    echo "Unknown runtime identifier: $rid (use linux-x64, osx-arm64 or osx-x64)" >&2
    exit 1
    ;;
esac

echo "Done: $archive ($(du -h "$archive" | cut -f1))"
