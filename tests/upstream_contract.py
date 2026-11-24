#!/usr/bin/env python3
"""Read-only compatibility check against VibePollo's live GitHub release metadata.

No packages are downloaded or installed. Intended for a scheduled CI job; deliberately
separate from the offline unit tests so normal PR checks don't depend on network access.
"""
from __future__ import annotations

import importlib.machinery
import importlib.util
from pathlib import Path
import re
from urllib.parse import urlsplit


def load_updater():
    path = Path(__file__).resolve().parents[1] / "vibepollo-updater"
    loader = importlib.machinery.SourceFileLoader("vibepollo_upstream_contract", str(path))
    spec = importlib.util.spec_from_loader(loader.name, loader)
    mod = importlib.util.module_from_spec(spec)
    loader.exec_module(mod)
    return mod


def _check_platform(m, raw: list[dict], recent: list[dict], *, windows: bool) -> str:
    label = "Windows" if windows else "Linux"
    pat = m.WIN_SETUP_RE if windows else m.PKG_RE
    checked = 0
    for release in recent:
        assets = release.get("assets") or []
        matching = [a for a in assets if isinstance(a, dict) and pat.fullmatch(str(a.get("name") or ""))]
        if not matching:
            continue  # Some upstream releases may omit one platform's asset.
        entry = m.release_entry(release, windows=windows)
        if entry is None:
            raise ValueError(f"Unrecognized upstream {label} release metadata: {release.get('tag_name', '(missing tag)')}")
        asset = entry["asset"]
        if not re.fullmatch(r"[a-f0-9]{64}", asset.get("sha256") or ""):
            raise ValueError(f"Missing or malformed {label} SHA-256: {entry['tag']}")
        uri = urlsplit(asset["url"])
        expected_path = f"/{m.REPO}/releases/download/"
        if (uri.scheme != "https" or uri.hostname != "github.com"
                or not uri.path.startswith(expected_path)
                or uri.username is not None or uri.password is not None):
            raise ValueError(f"Unexpected upstream {label} download URL: {entry['tag']}")
        checked += 1

    if not checked:
        raise ValueError(f"No usable {label} assets found in recent releases")
    channels = m.parse_channels(raw, windows=windows)
    if not channels["latest"]:
        raise ValueError(f"Cannot select the latest installable {label} release")
    if not channels["stable"]:
        raise ValueError(f"Cannot select a stable installable {label} release")
    return f"{label}: {checked} assets (stable={channels['stable']['tag']}, latest={channels['latest']['tag']})"


def verify_live_releases() -> None:
    m = load_updater()
    raw = m.fetch_releases(timeout=20.0)
    if not raw:
        raise ValueError("GitHub returned no VibePollo releases")

    recent = [r for r in raw if isinstance(r, dict) and r.get("draft") is not True][:20]
    if not recent:
        raise ValueError("No published GitHub releases among the first 20 results")

    linux_summary = _check_platform(m, raw, recent, windows=False)
    win_summary = _check_platform(m, raw, recent, windows=True)
    print(f"OK: {linux_summary} | {win_summary}")
    print("Metadata only: no package downloads, installations, or repo changes.")


if __name__ == "__main__":
    try:
        verify_live_releases()
    except Exception as exc:
        raise SystemExit(f"UPSTREAM CONTRACT FAILURE: {exc}") from exc
