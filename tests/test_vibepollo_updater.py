"""Unit tests for vibepollo-updater (channel classification, SHA-256 verification, cache pruning)."""
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
            }
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
            }
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
            }
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
            }
        ],
    },
]


class VibepolloUpdaterTests(unittest.TestCase):
    def test_parse_channels_picks_stable_beta_nightly_and_ignores_drafts(self) -> None:
        m = _load_mod()
        ch = m.parse_channels(SAMPLE_RELEASES)
        self.assertEqual(ch["stable"]["tag"], "2.18.9")
        self.assertEqual(ch["beta"]["tag"], "2.19.0-beta.1")
        self.assertEqual(ch["nightly"]["tag"], "2.20.1")
        self.assertEqual(ch["stable"]["asset"]["sha256"], "c" * 64)

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
        with tempfile.TemporaryDirectory() as td:
            d = Path(td)
            keep_other = d / "notes.txt"
            keep_other.write_text("keep", encoding="utf-8")
            created = []
            now = time.time()
            for i in range(5):
                f = d / f"vibepollo-2.18.{i}-1-x86_64.pkg.tar.zst"
                f.write_bytes(b"x")
                os.utime(f, (now - (5 - i) * 10, now - (5 - i) * 10))
                created.append(f)
            m.prune_cache(cache_dir=d, max_cached=3)
            remaining = sorted(p.name for p in m.list_cached_packages(cache_dir=d))
            self.assertEqual(
                remaining,
                [
                    "vibepollo-2.18.2-1-x86_64.pkg.tar.zst",
                    "vibepollo-2.18.3-1-x86_64.pkg.tar.zst",
                    "vibepollo-2.18.4-1-x86_64.pkg.tar.zst",
                ],
            )
            self.assertTrue(keep_other.exists())

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


if __name__ == "__main__":
    unittest.main()
