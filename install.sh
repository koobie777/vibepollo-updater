#!/usr/bin/env bash
set -euo pipefail

BIN_DIR="${HOME}/.local/bin"
APP_DIR="${HOME}/.local/share/applications"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "${1:-}" = "--uninstall" ]; then
  rm -f "${BIN_DIR}/vibepollo-updater" \
        "${APP_DIR}/vibepollo-updater.desktop" \
        "${HOME}/Desktop/vibepollo-updater.desktop"
  echo "Removed vibepollo-updater launchers (cached packages in ~/.cache/vibepollo-updater kept)."
  exit 0
fi

mkdir -p "${BIN_DIR}" "${APP_DIR}"
install -m 755 "${SCRIPT_DIR}/vibepollo-updater" "${BIN_DIR}/vibepollo-updater"

sed "s|@EXEC@|${BIN_DIR}/vibepollo-updater|g" "${SCRIPT_DIR}/vibepollo-updater.desktop.in" > "${APP_DIR}/vibepollo-updater.desktop"
chmod 644 "${APP_DIR}/vibepollo-updater.desktop"

if [ -d "${HOME}/Desktop" ] && [ "${1:-}" != "--no-desktop" ]; then
  cp -f "${APP_DIR}/vibepollo-updater.desktop" "${HOME}/Desktop/vibepollo-updater.desktop"
  chmod 755 "${HOME}/Desktop/vibepollo-updater.desktop"
fi

echo "Installed vibepollo-updater to ${BIN_DIR}/vibepollo-updater"
echo "Run 'vibepollo-updater --check' or launch 'Vibepollo Updater' from your app menu / Desktop."
