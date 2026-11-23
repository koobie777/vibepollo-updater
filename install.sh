#!/usr/bin/env bash
set -euo pipefail

BIN_DIR="${HOME}/.local/bin"
APP_DIR="${HOME}/.local/share/applications"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

DESKTOP=1; VIBE_ICON=0
for a in "$@"; do case "$a" in
  --no-desktop) DESKTOP=0 ;;
  --with-vibepollo-icon) VIBE_ICON=1 ;;
  --uninstall) ;;
  *) echo "unknown option: $a (use --no-desktop, --with-vibepollo-icon, --uninstall)" >&2; exit 2 ;;
esac; done

if [ "${1:-}" = "--uninstall" ]; then
  rm -f "${BIN_DIR}/vibepollo-updater" \
        "${APP_DIR}/vibepollo-updater.desktop" \
        "${HOME}/Desktop/vibepollo-updater.desktop"
  echo "Removed vibepollo-updater launchers (cached packages in ~/.cache/vibepollo-updater kept)."
  exit 0
fi

mkdir -p "${BIN_DIR}" "${APP_DIR}"
install -m 755 "${SCRIPT_DIR}/vibepollo-updater" "${BIN_DIR}/vibepollo-updater"

# Vibepollo's package installs its icon as "apollo". Fall back to a stock icon when Vibepollo is not installed yet.
ICON=system-software-update
for f in /usr/share/icons/hicolor/scalable/apps/apollo.svg /usr/share/icons/hicolor/*/apps/apollo.png; do
  [ -e "$f" ] && { ICON=apollo; break; }
done
sed -e "s|@EXEC@|${BIN_DIR}/vibepollo-updater|g" -e "s|@ICON@|${ICON}|g" "${SCRIPT_DIR}/vibepollo-updater.desktop.in" > "${APP_DIR}/vibepollo-updater.desktop"
chmod 644 "${APP_DIR}/vibepollo-updater.desktop"

if [ -d "${HOME}/Desktop" ] && [ "$DESKTOP" = 1 ]; then
  cp -f "${APP_DIR}/vibepollo-updater.desktop" "${HOME}/Desktop/vibepollo-updater.desktop"
  chmod 755 "${HOME}/Desktop/vibepollo-updater.desktop"
fi

if [ "$VIBE_ICON" = 1 ]; then
  "${BIN_DIR}/vibepollo-updater" --desktop-icon on || echo "Vibepollo icon skipped: install Vibepollo first, then: vibepollo-updater --desktop-icon on"
fi

echo "Installed vibepollo-updater to ${BIN_DIR}/vibepollo-updater"
echo "Run 'vibepollo-updater --check' or launch 'Vibepollo Updater' from your app menu / Desktop."
