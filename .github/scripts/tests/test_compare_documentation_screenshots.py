"""Test comparison control flow with fake ImageMagick tools, not product screenshots."""

import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import shutil


SCRIPT = Path(__file__).resolve().parents[1] / "compare-documentation-screenshots.sh"
FAKE_TOOL = """#!/usr/bin/env python3
import os
from pathlib import Path
import sys

tool = Path(sys.argv[0]).name
args = sys.argv[1:]
if tool == 'compare':
    if args[-1] == 'null:':
        assert args[:6] == ['-precision', '17', '-metric', 'AE', '-fuzz', '3%'], args
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
        self.assertIn("no differences beyond the colour tolerance", self.report.read_text())
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

    def test_invalid_or_unexpected_masks_fail_closed(self):
        mask = self.candidates / "fixture.png.mask"
        mask.write_text("0 0 100 100\n")
        result = self.run_comparison("0", status=0)
        self.assertNotIn("No material screenshot changes", result.stdout)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Unexpected map mask", result.stderr)

        (self.candidates / "fixture.png").rename(self.candidates / "client-workspace-light.png")
        (self.baselines / "fixture.png").rename(self.baselines / "client-workspace-light.png")
        mask.rename(self.candidates / "client-workspace-light.png.mask")
        (self.candidates / "client-workspace-light.png.mask").write_text("0 0 101 100\n")
        result = self.run_comparison("0", status=0)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Map mask or image dimensions invalid", result.stderr)


@unittest.skipUnless(all(shutil.which(tool) for tool in ("compare", "convert", "identify")), "ImageMagick is required")
class RealScreenshotComparisonTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="glovelly-real-screenshot-compare-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.candidates = self.root / "candidates"
        self.baselines = self.root / "baselines"
        self.report = self.root / "report" / "freshness.md"
        self.candidates.mkdir()
        self.baselines.mkdir()
        self.name = "client-workspace-light.png"

    def image(self, directory, background, *draw):
        subprocess.run(["convert", "-size", "100x100", f"xc:{background}", *draw, str(directory / self.name)], check=True)

    def compare(self, mask=None):
        if mask:
            (self.candidates / (self.name + ".mask")).write_text(mask)
        result = subprocess.run(["bash", str(SCRIPT), str(self.candidates), str(self.baselines), str(self.report)], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        return self.report.read_text()

    def test_tiny_colour_drift_does_not_request_review(self):
        self.image(self.baselines, "#808080")
        self.image(self.candidates, "#818181")
        self.assertIn("No material screenshot changes", self.compare())

    def test_meaningful_change_still_requests_review(self):
        self.image(self.baselines, "white")
        self.image(self.candidates, "white", "-fill", "black", "-draw", "rectangle 10,10 19,19")
        self.assertIn("candidate images need review", self.compare())

    def test_map_pixels_are_excluded_but_surrounding_changes_are_not(self):
        self.image(self.baselines, "white", "-fill", "red", "-draw", "rectangle 40,40 79,79")
        self.image(self.candidates, "white", "-fill", "blue", "-draw", "rectangle 40,40 79,79")
        self.assertIn("No material screenshot changes", self.compare("40 40 80 80\n"))
        self.image(self.candidates, "white", "-fill", "blue", "-draw", "rectangle 40,40 79,79", "-fill", "black", "-draw", "rectangle 10,10 19,19")
        self.assertIn("candidate images need review", self.compare("40 40 80 80\n"))

    def test_missing_map_is_not_masked(self):
        self.image(self.baselines, "white", "-fill", "red", "-draw", "rectangle 40,40 79,79")
        self.image(self.candidates, "white")
        self.assertIn("candidate images need review", self.compare())


if __name__ == "__main__":
    unittest.main()
