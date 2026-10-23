"""Test comparison control flow with fake ImageMagick tools, not product screenshots."""

import os
from pathlib import Path
import subprocess
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / "compare-documentation-screenshots.sh"
FAKE_TOOL = """#!/usr/bin/env python3
import os
from pathlib import Path
import sys

tool = Path(sys.argv[0]).name
args = sys.argv[1:]
if tool == 'compare':
    if args[-1] == 'null:':
        assert args[:4] == ['-precision', '17', '-metric', 'AE'], args
        assert os.environ['LC_ALL'] == 'C'
        sys.stderr.write(os.environ.get('FAKE_METRIC', '100'))
        sys.exit(int(os.environ.get('FAKE_COMPARE_STATUS', '1')))
    Path(args[-1]).write_bytes(b'fake diff artifact')
    sys.exit(int(os.environ.get('FAKE_REVIEW_STATUS', '1')))
elif tool == 'identify':
    print('100 100')
elif tool == 'convert':
    if args[-1] == 'info:':
        print('10x10+0+0')
    else:
        Path(args[-1]).write_bytes(b'fake overlay artifact')
"""


class ScreenshotComparisonTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="glovelly-screenshot-compare-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.bin = self.root / "bin"
        self.candidates = self.root / "candidates"
        self.baselines = self.root / "baselines"
        self.report = self.root / "review" / "freshness.md"
        for directory in (self.bin, self.candidates, self.baselines):
            directory.mkdir()
        for name in ("compare", "convert", "identify"):
            tool = self.bin / name
            tool.write_text(FAKE_TOOL)
            tool.chmod(0o755)
        (self.candidates / "fixture.png").write_bytes(b"fake candidate")
        (self.baselines / "fixture.png").write_bytes(b"fake baseline")

    def run_comparison(self, metric, status=1, **extra):
        env = dict(os.environ, PATH=f"{self.bin}{os.pathsep}{os.environ['PATH']}",
                   FAKE_METRIC=metric, FAKE_COMPARE_STATUS=str(status), **extra)
        return subprocess.run(["bash", str(SCRIPT), str(self.candidates), str(self.baselines), str(self.report)],
                              env=env, capture_output=True, text=True)

    def test_scientific_notation_produces_review_report_and_artifacts(self):
        for metric in ("1.40969e+06", "1.409689E6", "1409689"):
            with self.subTest(metric=metric):
                result = self.run_comparison(metric)
                self.assertEqual(result.returncode, 0, result.stderr)
                count = "1409690" if metric == "1.40969e+06" else "1409689"
                self.assertIn(f"changed: {count} pixels; threshold: 5", self.report.read_text())
                for suffix in ("diff", "overlay", "overlay-crop"):
                    self.assertTrue((self.report.parent / "diffs" / f"fixture-{suffix}.png").exists())

    def test_zero_is_encoding_only(self):
        result = self.run_comparison("0", status=0)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("differ only in PNG encoding", self.report.read_text())
        self.assertFalse((self.report.parent / "diffs").exists())
        self.assertEqual(self.report.with_suffix(".txt").read_text(), "")

    def test_identical_files_skip_pixel_comparison(self):
        (self.baselines / "fixture.png").write_bytes(b"fake candidate")
        result = self.run_comparison("must not be parsed", status=2)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("No material screenshot changes", self.report.read_text())
        self.assertEqual(self.report.with_suffix(".txt").read_text(), "")

    def test_missing_baseline_is_a_new_candidate(self):
        (self.baselines / "fixture.png").unlink()
        result = self.run_comparison("must not be parsed", status=2)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("fixture.png (new candidate)", self.report.read_text())

    def test_threshold_is_preserved(self):
        for metric, section in (("5e0", "within the rasterisation tolerance"), ("6", "candidate images need review")):
            with self.subTest(metric=metric):
                result = self.run_comparison(metric)
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertIn(section, self.report.read_text())

    def test_genuine_compare_failure_is_not_accepted_as_a_count(self):
        result = self.run_comparison("100", status=2)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("exit 2", result.stderr)

    def test_invalid_counts_fail(self):
        for metric in ("", "warning: 100", "-1", "1.5", "NaN", "Infinity", "1e999", "9007199254740992"):
            with self.subTest(metric=metric):
                result = self.run_comparison(metric)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("Invalid decoded-pixel count", result.stderr)

    def test_review_artifact_error_is_not_ignored(self):
        result = self.run_comparison("100", FAKE_REVIEW_STATUS="2")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Could not create visual diff", result.stderr)


if __name__ == "__main__":
    unittest.main()
