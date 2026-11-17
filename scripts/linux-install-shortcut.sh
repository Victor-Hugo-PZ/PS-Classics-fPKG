#!/usr/bin/env bash
# Adds "PS Classics fPKG Builder" to the applications menu of the current user (no root needed).
# Run it from the extracted release folder. Run it again if you move the folder.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
apps="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
mkdir -p "$apps"

cat > "$apps/ps-classics-fpkg-builder.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=PS Classics fPKG Builder
Comment=Create PS1, PS2 and PSP fake packages for PS4/PS5
Exec="$here/PSClassicsfPKGBuilder"
Path=$here
Icon=$here/app.png
Terminal=false
Categories=Utility;Game;
EOF

chmod +x "$apps/ps-classics-fpkg-builder.desktop"
command -v update-desktop-database >/dev/null && update-desktop-database "$apps" >/dev/null 2>&1 || true
echo "Shortcut added: look for 'PS Classics fPKG Builder' in your applications menu."
