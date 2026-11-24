"""Unit tests for vibepollo-updater (channel classification, SHA-256 verification, cache pruning, Windows & Linux)."""
from __future__ import annotations

import hashlib
import importlib.machinery
import importlib.util
import os
from pathlib import Path
import tempfile
import time
import unittest

ROOT = Path(__file__).resolve().parents[1]


def _load_mod():
    loader = importlib.machinery.SourceFileLoader("vibepollo_updater", str(ROOT / "vibepollo-updater"))
    spec = importlib.util.spec_from_loader(loader.name, loader)
    mod = importlib.util.module_from_spec(spec)
    loader.exec_module(mod)
    return mod


SAMPLE_RELEASES = [
    {
        "tag_name": "2.20.1",
        "name": "Vibepollo 2.20.1 (Nightly)",
        "prerelease": True,
        "draft": False,
        "published_at": "2026-11-22T18:08:58Z",
        "html_url": "https://github.com/Nonary/Vibepollo/releases/tag/2.20.1",
        "body": "Nightly fixes",
        "assets": [
            {
                "name": "vibepollo-2.20.1-1-x86_64.pkg.tar.zst",
                "browser_download_url": "https://github.com/Nonary/Vibepollo/releases/download/2.20.1/vibepollo-2.20.1-1-x86_64.pkg.tar.zst",
                "size": 192000000,
                "digest": "sha256:" + ("a" * 64),
            },
            {
                "name": "VibepolloSetup.exe",
                "browser_download_url": "https://github.com/Nonary/Vibepollo/releases/download/2.20.1/VibepolloSetup.exe",
                "size": 47800000,
                "digest": "sha256:" + ("1" * 64),
            },
        ],
    },
    {
        "tag_name": "2.19.0-beta.1",
        "name": "Vibepollo 2.19.0 Beta 1",
        "prerelease": True,
        "draft": False,
        "published_at": "2026-11-21T10:00:00Z",
        "html_url": "https://github.com/Nonary/Vibepollo/releases/tag/2.19.0-beta.1",
        "body": "Beta candidate",
        "assets": [
            {
                "name": "vibepollo-2.19.0beta1-1-x86_64.pkg.tar.zst",
                "browser_download_url": "https://github.com/Nonary/Vibepollo/releases/download/2.19.0-beta.1/vibepollo-2.19.0beta1-1-x86_64.pkg.tar.zst",
                "size": 191000000,
                "digest": "sha256:" + ("b" * 64),
            },
            {
                "name": "VibepolloSetup-v2.19.0-beta.1.exe",
                "browser_download_url": "https://github.com/Nonary/Vibepollo/releases/download/2.19.0-beta.1/VibepolloSetup-v2.19.0-beta.1.exe",
                "size": 47500000,
                "digest": "sha256:" + ("2" * 64),
            },
        ],
    },
    {
        "tag_name": "2.18.9",
        "name": "v2.18.9",
        "prerelease": False,
        "draft": False,
        "published_at": "2026-11-19T03:47:27Z",
        "html_url": "https://github.com/Nonary/Vibepollo/releases/tag/2.18.9",
        "body": "Stable release",
        "assets": [
            {
                "name": "vibepollo-2.18.9-1-x86_64.pkg.tar.zst",
                "browser_download_url": "https://github.com/Nonary/Vibepollo/releases/download/2.18.9/vibepollo-2.18.9-1-x86_64.pkg.tar.zst",
                "size": 191881011,
                "digest": "sha256:" + ("c" * 64),
            },
            {
                "name": "VibepolloSetup.exe",
                "browser_download_url": "https://github.com/Nonary/Vibepollo/releases/download/2.18.9/VibepolloSetup.exe",
                "size": 47600000,
                "digest": "sha256:" + ("3" * 64),
            },
        ],
    },
    {
        "tag_name": "2.99.0-draft",
        "name": "Draft",
        "prerelease": False,
        "draft": True,
        "assets": [
            {
                "name": "vibepollo-2.99.0-1-x86_64.pkg.tar.zst",
                "browser_download_url": "https://example.invalid/draft",
                "size": 1,
            },
            {
                "name": "VibepolloSetup.exe",
                "browser_download_url": "https://example.invalid/draft.exe",
                "size": 1,
            },
        ],
    },
]


class VibepolloUpdaterTests(unittest.TestCase):
    def test_channels_use_github_prerelease_status_not_numeric_suffix(self) -> None:
        m = _load_mod()
        for is_win, expected_stable_sha in ((False, "c" * 64), (True, "3" * 64)):
            ch = m.parse_channels(SAMPLE_RELEASES, windows=is_win)
            self.assertEqual(ch["stable"]["tag"], "2.18.9")
            self.assertEqual(ch["latest"]["tag"], "2.20.1")
            self.assertTrue(ch["latest"]["prerelease"])
            self.assertEqual(ch["stable"]["asset"]["sha256"], expected_stable_sha)
            self.assertEqual(m.select_channel(SAMPLE_RELEASES, "prerelease", windows=is_win)["tag"], "2.20.1")
            self.assertEqual(m.select_channel(SAMPLE_RELEASES, "nightly", windows=is_win)["tag"], "2.20.1")
            # A numeric prerelease with no "nightly" in its name must still be selected.
            newer = dict(SAMPLE_RELEASES[0], tag_name="2.24.0", name="Vibepollo 2.24.0",
                         published_at="2026-11-24T09:00:00Z")
            self.assertEqual(m.parse_channels([newer] + SAMPLE_RELEASES, windows=is_win)["latest"]["tag"], "2.24.0")
            # A misleading title must not override GitHub's stable flag.
            stable = dict(newer, name="Vibepollo 2.24.0 Nightly", prerelease=False)
            self.assertEqual(m.classify_channel(stable), "stable")

    def test_missing_release_fields_are_not_guessed_as_stable(self) -> None:
        m = _load_mod()
        valid = SAMPLE_RELEASES[0]
        for change in (
            {"draft": None},
            {"prerelease": None},
            {"prerelease": "false"},
            {"tag_name": ""},
            {"published_at": ""},
        ):
            bad = dict(valid, **change)
            self.assertIsNone(m.release_entry(bad), change)
            self.assertFalse(m.parse_releases([bad]), change)
        self.assertEqual(m.release_entry(valid)["tag"], "2.20.1")

    def test_numeric_downgrade_is_explicit(self) -> None:
        m = _load_mod()
        old_stable = m.parse_channels(SAMPLE_RELEASES)["stable"]
        latest = m.parse_channels(SAMPLE_RELEASES)["latest"]
        self.assertTrue(m.release_is_downgrade("2.20.1-1", old_stable))
        self.assertTrue(m.release_is_downgrade("2.20.1", old_stable))
        self.assertFalse(m.release_is_downgrade("2.18.9-1", latest))
        self.assertFalse(m.release_is_downgrade("2.18.9", latest))
        self.assertFalse(m.release_is_downgrade(None, old_stable))
        self.assertFalse(m.release_is_downgrade("not-a-version", old_stable))
        self.assertEqual(m.numeric_version("2.20.1-1"), (2, 20, 1))
        self.assertEqual(m.numeric_version("1:2.20.1-1"), (2, 20, 1))

    def test_missing_release_digest_rejected_before_download(self) -> None:
        m = _load_mod()
        bad = dict(SAMPLE_RELEASES[0], asset=dict(m.release_entry(SAMPLE_RELEASES[0])["asset"], sha256=""))
        with tempfile.TemporaryDirectory() as td:
            with self.assertRaisesRegex(ValueError, "SHA-256"):
                m.download_and_verify(bad, cache_dir=Path(td))

    def test_parse_releases_lists_pre_releases_newest_first_and_can_hide_them(self) -> None:
        m = _load_mod()
        rows = m.parse_releases(SAMPLE_RELEASES)
        tags = [e["tag"] for e in rows]
        self.assertIn("2.19.0-beta.1", tags)
        self.assertIn("2.20.1", tags)
        self.assertEqual(rows, sorted(rows, key=lambda e: e["published_at"], reverse=True))
        stable = m.parse_releases(SAMPLE_RELEASES, include_prereleases=False)
        self.assertTrue(stable)
        self.assertTrue(all(not e["prerelease"] for e in stable))
        self.assertFalse(any(e["tag"] == "2.19.0-beta.1" for e in stable))

    def test_find_release_matches_exact_and_v_prefixed_tags_and_skips_drafts(self) -> None:
        m = _load_mod()
        self.assertEqual(m.find_release(SAMPLE_RELEASES, "2.19.0-beta.1")["tag"], "2.19.0-beta.1")
        self.assertEqual(m.find_release(SAMPLE_RELEASES, "v2.18.9")["tag"], "2.18.9")
        self.assertIsNone(m.find_release(SAMPLE_RELEASES, "9.9.9"))
        drafts = [r["tag_name"] for r in SAMPLE_RELEASES if r.get("draft")]
        for t in drafts:
            self.assertIsNone(m.find_release(SAMPLE_RELEASES, t))

    def test_linux_family_accepts_arch_relatives_and_refuses_debian(self) -> None:
        m = _load_mod()
        self.assertEqual(m.linux_family('ID=cachyos\nID_LIKE=arch\n'), "arch")
        self.assertEqual(m.linux_family('ID=endeavouros\nID_LIKE="arch"\n'), "arch")
        self.assertEqual(m.linux_family('ID=ubuntu\nID_LIKE=debian\n'), "other")
        self.assertEqual(m.linux_family('ID=fedora\n'), "other")
        self.assertEqual(m.linux_family(""), "other")

    def test_verify_file_sha256_accepts_match_and_rejects_mismatch(self) -> None:
        m = _load_mod()
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "pkg.tar.zst"
            data = b"vibepollo-test-payload"
            p.write_bytes(data)
            good = hashlib.sha256(data).hexdigest()
            self.assertEqual(m.verify_file_sha256(p, good), good)
            with self.assertRaises(ValueError):
                m.verify_file_sha256(p, "0" * 64)

    def test_prune_cache_keeps_newest_three_and_ignores_unrelated_files(self) -> None:
        m = _load_mod()
        # Linux packages
        with tempfile.TemporaryDirectory() as td:
            d = Path(td)
            keep_other = d / "notes.txt"
            keep_other.write_text("keep", encoding="utf-8")
            now = time.time()
            for i in range(5):
                f = d / f"vibepollo-2.18.{i}-1-x86_64.pkg.tar.zst"
                f.write_bytes(b"x")
                os.utime(f, (now - (5 - i) * 10, now - (5 - i) * 10))
            m.prune_cache(cache_dir=d, max_cached=3, windows=False)
            remaining = sorted(p.name for p in m.list_cached_packages(cache_dir=d, windows=False))
            self.assertEqual(
                remaining,
                [
                    "vibepollo-2.18.2-1-x86_64.pkg.tar.zst",
                    "vibepollo-2.18.3-1-x86_64.pkg.tar.zst",
                    "vibepollo-2.18.4-1-x86_64.pkg.tar.zst",
                ],
            )
            self.assertTrue(keep_other.exists())
        # Windows tagged setup executables
        with tempfile.TemporaryDirectory() as td:
            d = Path(td)
            keep_other = d / "notes.txt"
            keep_other.write_text("keep", encoding="utf-8")
            now = time.time()
            for i in range(5):
                f = d / f"VibepolloSetup-2.18.{i}.exe"
                f.write_bytes(b"x")
                os.utime(f, (now - (5 - i) * 10, now - (5 - i) * 10))
            m.prune_cache(cache_dir=d, max_cached=3, windows=True)
            remaining = sorted(p.name for p in m.list_cached_packages(cache_dir=d, windows=True))
            self.assertEqual(
                remaining,
                [
                    "VibepolloSetup-2.18.2.exe",
                    "VibepolloSetup-2.18.3.exe",
                    "VibepolloSetup-2.18.4.exe",
                ],
            )
            self.assertTrue(keep_other.exists())

    def test_windows_asset_naming_and_matches_installed(self) -> None:
        m = _load_mod()
        win_rel = m.release_entry(SAMPLE_RELEASES[0], windows=True)
        self.assertIsNotNone(win_rel)
        self.assertEqual(win_rel["asset"]["name"], "VibepolloSetup.exe")
        self.assertEqual(m.cached_name(win_rel, windows=True), "VibepolloSetup-2.20.1.exe")
        self.assertTrue(m.matches_installed_release("2.20.1", win_rel))
        self.assertFalse(m.matches_installed_release("2.18.9", win_rel))
        lin_rel = m.release_entry(SAMPLE_RELEASES[0], windows=False)
        self.assertEqual(m.cached_name(lin_rel, windows=False), "vibepollo-2.20.1-1-x86_64.pkg.tar.zst")
        self.assertTrue(m.matches_installed_release("2.20.1-1", lin_rel))

    def test_windows_install_summary_handles_system_component_and_stacked_installs(self) -> None:
        m = _load_mod()
        self.assertEqual(m.summarize_windows_installs([]), (None, False))
        # Healthy install: 1 visible bootstrapper + 1 hidden MSI (SystemComponent=1)
        healthy = [
            {"version": "2.23.1", "msi": False, "system_component": False},
            {"version": "2.23.199.0", "msi": True, "system_component": True},
        ]
        self.assertEqual(m.summarize_windows_installs(healthy), ("2.23.1", False))
        # Stacked install: two visible entries (or two MSIs) -> warn
        stacked = [
            {"version": "2.2.0", "msi": False, "system_component": False},
            {"version": "2.2.99.0", "msi": True, "system_component": False},
        ]
        self.assertEqual(m.summarize_windows_installs(stacked), ("2.2.99.0", True))
        # MSI-only entry still reports its version without false stacking
        msi_only = [{"version": "2.18.9.0", "msi": True, "system_component": True}]
        self.assertEqual(m.summarize_windows_installs(msi_only), ("2.18.9.0", False))

    def test_vigembus_summary_and_pinned_metadata(self) -> None:
        m = _load_mod()
        self.assertEqual(m.VIGEMBUS_VERSION, "1.22.0")
        self.assertRegex(m.VIGEMBUS_SHA256, r"^[0-9a-f]{64}$")
        self.assertTrue(m.VIGEMBUS_URL.startswith("https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/"))
        missing = m.summarize_vigembus(None, "not installed")
        self.assertFalse(missing["installed"])
        self.assertFalse(missing["needs_update"])
        self.assertIn("not installed", missing["summary"])
        older = m.summarize_vigembus("1.17.333", "running")
        self.assertTrue(older["installed"])
        self.assertTrue(older["needs_update"])
        self.assertIn("twice", older["summary"])
        current = m.summarize_vigembus("1.22.0", "running")
        self.assertTrue(current["installed"])
        self.assertFalse(current["needs_update"])
        self.assertEqual(current["summary"], "1.22.0 (running)")

    def test_parse_safe_vibepollo_conflicts_allows_only_vibepollo_vibeshine_paths(self) -> None:
        m = _load_mod()
        good_out = (
            "error: failed to commit transaction (conflicting files)\n"
            "vibepollo: /usr/lib/libvibeshine-kwin-gpu.so exists in filesystem\n"
            "Errors occurred, no packages were upgraded.\n"
        )
        self.assertEqual(
            m.parse_safe_vibepollo_conflicts(good_out),
            ["/usr/lib/libvibeshine-kwin-gpu.so"],
        )
        # Must fail closed if ANY conflicting file is outside vibepollo/vibeshine or traverses ..
        for bad_out in (
            "vibepollo: /usr/lib/libc.so.6 exists in filesystem\n",
            "vibepollo: /etc/vibepollo.conf exists in filesystem\n",
            "vibepollo: /usr/lib/../etc/vibepollo exists in filesystem\n",
            "vibepollo: /usr/lib/libvibeshine-kwin-gpu.so exists in filesystem\notherpkg: /usr/bin/foo exists in filesystem\n",
        ):
            self.assertEqual(m.parse_safe_vibepollo_conflicts(bad_out), [], bad_out)

    def test_vibepollo_desktop_icon_on_off_and_missing_source(self) -> None:
        m = _load_mod()
        with tempfile.TemporaryDirectory() as td:
            src = Path(td) / "io.github.Nonary.vibepollo.desktop"
            dst = Path(td) / "Desktop" / "io.github.Nonary.vibepollo.desktop"
            src.write_text("[Desktop Entry]\nName=Vibepollo\nIcon=apollo.svg\n", encoding="utf-8")
            if m.WINDOWS:
                self.assertFalse(m.vibepollo_desktop_icon(src=src, dst=dst))
                with self.assertRaises(OSError):
                    m.vibepollo_desktop_icon("on", src=src, dst=dst)
            else:
                self.assertFalse(m.vibepollo_desktop_icon(src=src, dst=dst))
                self.assertTrue(m.vibepollo_desktop_icon("on", src=src, dst=dst))
                self.assertEqual(dst.stat().st_mode & 0o777, 0o755)
                self.assertIn("Icon=apollo\n", dst.read_text(encoding="utf-8"))
                self.assertFalse(m.vibepollo_desktop_icon("off", src=src, dst=dst))
                with self.assertRaises(FileNotFoundError):
                    m.vibepollo_desktop_icon("on", src=Path(td) / "missing.desktop", dst=dst)


if __name__ == "__main__":
    unittest.main()
