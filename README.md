# Vibepollo Installer & Updater (Arch Linux GUI + CLI)

A lightweight, standalone **PySide6 GUI + CLI installer and updater** for **[Nonary/Vibepollo](https://github.com/Nonary/Vibepollo)** on Arch Linux and Arch-based distros (EndeavourOS, CachyOS, Manjaro, SteamOS).

Upstream's Linux beta publishes an `x86_64` pacman package and a Windows installer. It does **not** publish Debian, Fedora, AppImage, or Flatpak builds. On those distros this tool still shows every channel and version, and it refuses to install rather than fetching a package that does not exist. See [upstream's Linux install guide](https://github.com/Nonary/Vibepollo/blob/master/docs/linux/install.md).

Instead of manually hunting through GitHub releases and running `pacman -U` by hand, pick **Stable**, **Latest**, or any **specific version** (pre-releases included), inspect release notes, verify the official SHA-256 digest, and install or roll back.

**Upstream release model (November 2026):** VibePollo publishes numeric `2.x.x` versions across release types. The numeric tag alone does not identify stability; GitHub's `prerelease` metadata does. **Stable is time-gated** and can be numerically *older* than Latest. The updater detects this and warns before a downgrade (CLI requires `--allow-downgrade`).

---

## Features

- **Release selection based on upstream metadata:**
  - **Stable** — newest GitHub release *not* marked as prerelease (time-gated).
  - **Latest** — newest published GitHub release, including prereleases.
  - **Specific version** — install a chosen tag from the last 100 releases; toggle *Show pre-releases* to filter.
  - **Legacy CLI aliases** — `--update prerelease` and `--update nightly` both select the newest published prerelease, regardless of tag naming. Deprecated as UI categories; `--update beta` is not supported for the new numeric release stream.
- **Downgrade protection:** Warns in the GUI when the selected `2.x.x` version is older than the installed version. The CLI refuses unless you explicitly add `--allow-downgrade`. Intentional rollback remains available.
- **Mandatory SHA-256 Verification:** Checks every downloaded `vibepollo-*-x86_64.pkg.tar.zst` against the official GitHub release digest before invoking `pacman -U` (also refuses missing or malformed digests).
- **Smart Channel-Switch Conflict Handling:** When switching between Stable and Nightly (for example, an untracked `/usr/lib/libvibeshine-kwin-gpu.so` left behind by an earlier build), detects unowned `vibepollo`/`vibeshine` files via `pacman -Qo` and offers a strictly scoped `--overwrite` for ONLY those exact unowned files (never blind `--overwrite '*'`).
- **1-Click Rollback:** Automatically caches the last 3 verified `.pkg.tar.zst` packages in `~/.cache/vibepollo-updater/` so switching back from a Nightly takes one click (or one command).
- **Clean Native Elevation:** Uses `pkexec pacman -U` in GUI mode (standard Polkit / KDE / GNOME password prompt) and `sudo pacman -U` in CLI mode — never runs an unpinned remote shell script and never touches system files until you confirm.
- **Desktop + CLI Parity:** Use the dark-themed PySide6 GUI from your app menu/Desktop, or run headless from any terminal with `--check`, `--json`, `--list`, `--update`, `--install-version`, and `--rollback`.
- **Zero Extra Dependencies:** Pure Python 3 standard library for CLI mode; uses the `pyside6` package that Vibepollo itself already depends on on Arch!

---

## Quick Install

```bash
git clone https://github.com/koobie777/vibepollo-updater.git
cd vibepollo-updater
./install.sh
```

This installs:
- `~/.local/bin/vibepollo-updater`
- `~/.local/share/applications/vibepollo-updater.desktop` (App Menu launcher using Vibepollo's official icon)
- `~/Desktop/vibepollo-updater.desktop` (pass `./install.sh --no-desktop` to skip the Desktop shortcut)
- Optional: a **Vibepollo** launcher on the Desktop too. `./install.sh --with-vibepollo-icon`, the *Vibepollo icon on the Desktop* box in the window, or `vibepollo-updater --desktop-icon on|off|status`. `--uninstall` leaves it; turn it off with `--desktop-icon off`.

To uninstall:
```bash
./install.sh --uninstall
```

---

## Usage

### GUI Mode
Launch **Vibepollo Updater** from your Application Menu or Desktop, or run:
```bash
vibepollo-updater
```

### CLI Mode
```bash
# Compare installed Vibepollo against Stable and Latest (including downgrade indicators)
vibepollo-updater --check

# Machine-readable JSON output
vibepollo-updater --json

# Download, verify SHA-256, and install a channel
vibepollo-updater --update stable
vibepollo-updater --update latest
vibepollo-updater --update prerelease  # legacy alias for latest pre-release
vibepollo-updater --update nightly     # same legacy alias

# Only when deliberately switching to an older numeric version:
vibepollo-updater --update stable --allow-downgrade

# Roll back to the most recent cached package
vibepollo-updater --rollback

# Every release, pre-releases included (add --stable-only to hide them)
vibepollo-updater --list

# Install one specific release, stable or pre-release
vibepollo-updater --install-version 2.20.1
```

### Example `--check` Output
```text
Vibepollo Installed : 2.20.1-1 (controller=active · host=active)
Upstream Repo       : https://github.com/Nonary/Vibepollo
  stable    : 2.18.9       [stable]  (2026-11-19, 183.0 MiB)  [OLDER THAN INSTALLED]
  latest    : 2.23.1       [pre-release]  (2026-11-23, 183.3 MiB)
Older and specific versions: vibepollo-updater --list
```

---

## How Safety & Verification Work

1. **HTTPS + Official Upstream Only:** Queries `https://api.github.com/repos/Nonary/Vibepollo/releases` directly and refuses non-HTTPS asset URLs.
2. **No Shell Execution:** Every `pacman` and `systemctl` call uses strict argv lists (`shell=False`).
3. **SHA-256 Check Before Elevation:** Requires an official 64-digit SHA-256 `digest`, downloads to a `.part` temporary file, verifies before caching, and only then asks for elevation via `pkexec` / `sudo`.
4. **No Background Daemons or Auto-Updates:** Only runs when you explicitly open it or invoke it from the terminal. The window stays open while a check or install is running, and a close mid-job is ignored so the worker thread is not destroyed.

---

## Requirements

- Arch Linux / EndeavourOS / CachyOS / Manjaro (`x86_64`, `pacman`)
- Python 3.10+
- `pyside6` (for GUI mode; already installed as a dependency of `vibepollo`)
- `polkit` (`pkexec`) or `sudo`

---

## Development & Tests

```bash
python3 -m unittest discover -s tests -v
QT_QPA_PLATFORM=offscreen ./vibepollo-updater --smoke-offscreen
```

---

## Disclaimer & License

This is an unofficial community helper utility for **[Nonary/Vibepollo](https://github.com/Nonary/Vibepollo)**. Vibepollo itself is owned and maintained by its upstream developers under GPL-3.0-only.

This updater tool is licensed under the **MIT License** — see [LICENSE](LICENSE).
