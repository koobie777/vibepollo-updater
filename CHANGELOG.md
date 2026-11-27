# Changelog

All notable changes to **vibepollo-updater** are documented in this file. Each version is also a [GitHub Release](https://github.com/koobie777/vibepollo-updater/releases). Update this file in the same commit as the version bump.

## [1.4.0] — 2026-11-26
### Added
- **WinUI 3 + Uno Skia window** (`app/VibepolloUi`) for Windows and Linux. The Python CLI still does every download, SHA-256 check, and install.
- Check JSON is `vibepollo.updater.check.v2`: each release has an action (`install`, `update`, `reinstall`, `downgrade`, `unavailable`), whether the SHA-256 is present, notes, and the newest config-backup names.
- `--rollback-cached NAME` reinstalls one file already in the cache. A path is refused.
- `--self-check` is offline. It prints the updater version and platform and installs nothing.
- The window shows Stable and Latest and the plan (installed version to the selected release). It selects an update when one exists, otherwise the installed release, otherwise Stable. A downgrade stays a separate confirmed action. It also shows a stacked-install warning, silent Windows install, and a choice of cached rollback. A newer copy of the Python engine wins over an older one.
- GitHub release assets: a Windows zip (`install.ps1` plus the WinUI window), a Linux tar (`install.sh` plus the CLI), and a screenshot of the 1.4.0 window. Uninstall removes the window folder and leaves the download cache and config backups.

## [1.3.1] — 2026-11-24
### Changed
- **Refreshed Dark Outer-Space GUI:** Upgraded PySide6 stylesheet with deeper contrast cards, cyan gradient progress bar, crisp hover/focus states, custom slim scrollbars, and native Windows (`Segoe UI` / `Cascadia Mono`) + Linux (`Inter` / `JetBrains Mono`) font stacks.
- Added Windows `CREATE_NO_WINDOW` subprocess helper to prevent stray console flashes.
- Added complete project `CHANGELOG.md`.

## [1.3.0] — 2026-11-24
### Added
- **Windows Support:** Full Windows installer/updater support (`VibepolloSetup*.exe`), automatic config backup prior to install, elevated single-UAC install flow, and `install.ps1`.
- **ViGEmBus v1.22.0 Helper:** Detects installed ViGEmBus on Windows and installs the pinned EOL `v1.22.0` release with verified SHA-256 checksum.
- Cross-platform Windows + Ubuntu CI test matrix (`tests/test_vibepollo_updater.py`).

## [1.2.0] — 2026-11-23
### Added
- Upstream release compatibility watch and community README polish (#3).
- Numeric release channel adaptation using GitHub release pre-release metadata and explicit downgrade guards (#2).

## [1.1.1] — 2026-11-23
### Fixed
- Desktop launcher icon resolution and optional Vibepollo Desktop icon toggle (#1).

## [1.1.0] — 2026-11-23
### Added
- Pre-release channel support, full release listing (`--list`), and install-any-tag (`--install-version`).

## [1.0.0] — 2026-11-23
### Added
- Initial Arch Linux PySide6 GUI + CLI updater for `Nonary/Vibepollo` with SHA-256 verification and 3-version rollback cache.
