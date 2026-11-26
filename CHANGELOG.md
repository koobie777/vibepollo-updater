# Changelog

All notable changes to **vibepollo-updater** are documented in this file. Each version is also a [GitHub Release](https://github.com/koobie777/vibepollo-updater/releases). Update this file in the same commit as the version bump.

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
