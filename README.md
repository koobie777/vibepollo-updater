# Vibepollo Updater (Arch Linux GUI + CLI)

A lightweight, standalone **PySide6 GUI + CLI updater** for **[Nonary/Vibepollo](https://github.com/Nonary/Vibepollo)** on Arch Linux and Arch-based distros (EndeavourOS, CachyOS, Manjaro, etc.).

Instead of manually hunting through GitHub releases and running `pacman -U` by hand, pick **Stable**, **Beta**, or **Nightly**, inspect the release notes, verify the official SHA-256 digest automatically, and install or roll back in one click.

---

## Features

- **3 Release Channels:**
  - **Stable** — latest non-prerelease GitHub release.
  - **Beta / Pre-release** — release candidates & beta builds whenever posted upstream.
  - **Nightly** — latest Nightly build with bleeding-edge fixes.
- **Mandatory SHA-256 Verification:** Checks every downloaded `vibepollo-*-x86_64.pkg.tar.zst` against the official GitHub release digest before invoking `pacman -U` (refuses to install and deletes the temp file on mismatch).
- **Smart Channel-Switch Conflict Handling:** When switching between Stable and Nightly (for example, an untracked `/usr/lib/libvibeshine-kwin-gpu.so` left behind by an earlier build), detects unowned `vibepollo`/`vibeshine` files via `pacman -Qo` and offers a strictly scoped `--overwrite` for ONLY those exact unowned files (never blind `--overwrite '*'`).
- **1-Click Rollback:** Automatically caches the last 3 verified `.pkg.tar.zst` packages in `~/.cache/vibepollo-updater/` so switching back from a Nightly takes one click (or one command).
- **Clean Native Elevation:** Uses `pkexec pacman -U` in GUI mode (standard Polkit / KDE / GNOME password prompt) and `sudo pacman -U` in CLI mode — never runs an unpinned remote shell script and never touches system files until you confirm.
- **Desktop + CLI Parity:** Use the dark-themed PySide6 GUI from your app menu/Desktop, or run headless from any terminal with `--check`, `--json`, `--update`, and `--rollback`.
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
# Compare your installed Vibepollo version against Stable, Beta, and Nightly
vibepollo-updater --check

# Machine-readable JSON output
vibepollo-updater --json

# Download, verify SHA-256, and install a channel
vibepollo-updater --update stable
vibepollo-updater --update beta
vibepollo-updater --update nightly

# Roll back to the most recent cached package
vibepollo-updater --rollback
```

### Example `--check` Output
```text
Vibepollo Installed : 2.18.9-1 (controller=active · host=active)
Upstream Repo       : https://github.com/Nonary/Vibepollo
  stable  : 2.18.9       [v2.18.9]  (2026-11-19, 183.0 MiB)
  beta    : (no dedicated beta release posted right now)
  nightly : 2.20.1       [Vibepollo 2.20.1 (Nightly)]  (2026-11-22, 183.2 MiB)
```

---

## How Safety & Verification Work

1. **HTTPS + Official Upstream Only:** Queries `https://api.github.com/repos/Nonary/Vibepollo/releases` directly and refuses non-HTTPS asset URLs.
2. **No Shell Execution:** Every `pacman` and `systemctl` call uses strict argv lists (`shell=False`).
3. **SHA-256 Check Before Elevation:** Downloads to a `.part` temporary file, verifies SHA-256 against GitHub's official asset `digest`, atomically renames into `~/.cache/vibepollo-updater/`, and only then asks for elevation via `pkexec` / `sudo`.
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
