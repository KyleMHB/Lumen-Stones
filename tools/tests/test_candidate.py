import importlib.util
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

from PIL import Image

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import artwork
from artwork_candidate import alphaEvidence, checkCandidate

spec = importlib.util.spec_from_file_location("attempts", TOOLS / "artwork-attempt.py")
attempts = importlib.util.module_from_spec(spec)
spec.loader.exec_module(attempts)


class CandidateTests(unittest.TestCase):
    def setUp(self):
        scratch = TOOLS.parent / "work"
        scratch.mkdir(exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=scratch)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / "tools").mkdir()
        shutil.copy2(TOOLS / "export-layered-artwork.py", self.root / "tools")
        (self.root / "Defs").mkdir()
        (self.root / "Defs/Test.xml").write_text(
            '<Defs><ThingDef><defName>Test</defName><graphicData><drawSize>(1,1.12)</drawSize></graphicData></ThingDef></Defs>')
        Image.new("RGBA", (8, 8), (180, 180, 180, 255)).save(self.root / "layer.png")
        self.manifest = dict(sourceSize=[8,8], crop=[0,0,8,8], canvas=[8,8], targetBox=[0,0,8,8],
            sourceRepair="none", alphaHandling="preserve", colorFilename="color.png", maskFilename="mask.png",
            layers=[dict(path="layer.png", constructionTint=True)], candidateChecks=dict(
                baseline={}, definition="Test", drawSize=[1,1.12], minSourcePixelsPerOutputPixel=1,
                minSourceSize=8, maxOpaqueSpread=8, boundsTolerance=0))
        self.path = self.root / "master.json"
        self.path.write_text(json.dumps(self.manifest))
        self.output = self.root / "output"
        import contextlib
        import io
        exporterSpec = importlib.util.spec_from_file_location("fixture_export", TOOLS / "export-layered-artwork.py")
        exporter = importlib.util.module_from_spec(exporterSpec)
        exporterSpec.loader.exec_module(exporter)
        with contextlib.redirect_stdout(io.StringIO()):
            exporter.export(self.path, self.output)
        for role, name in (("color", "color.png"), ("mask", "mask.png")):
            target = self.root / ("baseline-" + name)
            shutil.copy2(self.output / name, target)
            self.manifest["candidateChecks"]["baseline"][role] = dict(path=target.name, sha256=artwork.digest(target))
        self.path.write_text(json.dumps(self.manifest))

    def test_candidate_and_tampered_mask(self):
        self.assertTrue(checkCandidate(self.root, self.path, self.output)["passed"])
        Image.new("RGBA", (8,8), (0,0,0,255)).save(self.output / "mask.png")
        before = (self.output / "mask.png").read_bytes()
        report = checkCandidate(self.root, self.path, self.output)
        self.assertFalse(report["passed"])
        self.assertIn("Candidate mask", " ".join(report["errors"]))
        self.assertEqual(before, (self.output / "mask.png").read_bytes())

    def test_explicit_baseline_scale_preserves_normalized_placement(self):
        Image.new("RGBA", (16,16), (180,180,180,255)).save(self.root / "layer.png")
        self.manifest.update(sourceSize=[16,16], crop=[0,0,16,16], canvas=[16,16], targetBox=[0,0,16,16])
        self.manifest["candidateChecks"]["baselineScale"] = 2
        self.path.write_text(json.dumps(self.manifest))
        artwork.loadExporter(self.root).export(self.path, self.output)
        report = checkCandidate(self.root, self.path, self.output)
        self.assertTrue(report["passed"], report["errors"])
        self.assertEqual(report["expectedBounds"], (0,0,16,16))
        self.assertEqual(report["baselineBounds"], (0,0,8,8))

    def test_scaled_baseline_rejects_aspect_anchor_and_implicit_changes(self):
        for role, name in (("color", "color.png"), ("mask", "mask.png")):
            baseline = self.root / ("baseline-" + name)
            Image.new("RGBA", (4,4), (180,180,180,255)).save(baseline)
            self.manifest["candidateChecks"]["baseline"][role]["sha256"] = artwork.digest(baseline)
        self.path.write_text(json.dumps(self.manifest))
        self.assertIn("Candidate canvas", " ".join(checkCandidate(self.root, self.path, self.output)["errors"]))
        self.manifest["candidateChecks"]["baselineScale"] = 2
        self.manifest["canvas"] = [8,12]
        self.path.write_text(json.dumps(self.manifest))
        self.assertIn("Candidate canvas", " ".join(checkCandidate(self.root, self.path, self.output)["errors"]))
        self.manifest.update(canvas=[8,8], targetBox=[0,0,8,7])
        self.path.write_text(json.dumps(self.manifest))
        errors = " ".join(checkCandidate(self.root, self.path, self.output)["errors"])
        self.assertIn("Visible bounds", errors)
        self.assertIn("Foot line", errors)

    def test_baseline_placement_and_draw_failures_are_reported(self):
        self.manifest["candidateChecks"]["baseline"]["color"]["sha256"] = "wrong"
        self.manifest["layers"][0].update(scale=0.5, offset=[0,0])
        self.manifest["candidateChecks"]["drawSize"] = [2,2]
        self.path.write_text(json.dumps(self.manifest))
        errors = " ".join(checkCandidate(self.root, self.path, self.output)["errors"])
        for expected in ("Baseline color", "Visible bounds", "Foot line", "Draw size"):
            self.assertIn(expected, errors)

    def test_new_design_reports_unavailable_comparisons(self):
        checks = self.manifest["candidateChecks"]
        for key in ("baseline", "definition", "drawSize"):
            del checks[key]
        checks.update(mode="new-design", expectedBounds=[0,0,8,8], footLine=8,
                      materialOwnership="unresolved")
        self.path.write_text(json.dumps(self.manifest))
        report = checkCandidate(self.root, self.path, self.output)
        self.assertTrue(report["passed"])
        self.assertEqual(len(report["unavailable"]), 3)
        self.assertIsNone(report["baselineBounds"])
        checks.update(expectedBounds=[2,2,6,6], footLine=6)
        self.path.write_text(json.dumps(self.manifest))
        errors = " ".join(checkCandidate(self.root, self.path, self.output)["errors"])
        self.assertIn("Visible bounds", errors)
        self.assertIn("Foot line", errors)

    def test_detail_gate_uses_total_sampling_density(self):
        self.manifest["layers"][0].update(scale=0.5, offset=[0,0])
        self.manifest["candidateChecks"]["minSourcePixelsPerOutputPixel"] = 2
        self.path.write_text(json.dumps(self.manifest))
        report = checkCandidate(self.root, self.path, self.output)
        self.assertEqual(report["layers"][0]["sourcePixelsPerOutputPixel"], 2)
        self.assertNotIn("Insufficient source detail", " ".join(report["errors"]))
        self.manifest["candidateChecks"]["minSourcePixelsPerOutputPixel"] = 3
        self.path.write_text(json.dumps(self.manifest))
        self.assertIn("Insufficient source detail", " ".join(
            checkCandidate(self.root, self.path, self.output)["errors"]))

    def test_faint_alpha_is_reported_without_cleanup(self):
        image = Image.new("RGBA", (8,8))
        image.putpixel((4,4), (100,100,100,255))
        image.putpixel((0,0), (200,200,200,1))
        original = image.tobytes()
        evidence = alphaEvidence(image)
        self.assertEqual(evidence["boundsAboveAlpha"]["0"], (0,0,5,5))
        self.assertEqual(evidence["boundsAboveAlpha"]["8"], (4,4,5,5))
        self.assertEqual(image.tobytes(), original)
        image.save(self.root / "layer.png")
        self.manifest["candidateChecks"]["alphaDebrisTolerance"] = 1
        self.path.write_text(json.dumps(self.manifest))
        self.assertTrue(checkCandidate(self.root, self.path, self.output)["warnings"])

    def test_alpha_evidence_exposes_sparse_opaque_sample(self):
        image = Image.new("RGBA", (2,1), (255,0,0,254))
        image.putpixel((0,0), (100,100,100,255))
        report = alphaEvidence(image)
        self.assertEqual(report["opaqueFractionOfVisible"], 0.5)
        self.assertEqual(report["opaqueMeanSpread"], 0)
        self.assertGreater(report["alphaWeightedMeanSpread"], 100)

    def test_attempt_requires_capture_and_changes_method_after_two_defects(self):
        ledger = self.root / "attempts"
        request = dict(prompt="exact prompt", referenced_image_paths=[str(self.root / "layer.png")])
        first = attempts.begin(ledger, "bowl", request)
        with self.assertRaisesRegex(ValueError, "pending"):
            attempts.begin(ledger, "bowl", request)
        for index in range(2):
            path = first if index == 0 else attempts.begin(ledger, "bowl", request)
            retained = attempts.finish(path, self.root / "layer.png", "rim missing", "missing-rim")
            self.assertTrue(retained.exists())
        with self.assertRaisesRegex(ValueError, "change the method"):
            attempts.begin(ledger, "bowl", request)
        attempts.begin(ledger, "bowl", request, "supply isolated rim reference")

    def test_failed_generation_is_recorded_before_retry(self):
        ledger = self.root / "failed-attempts"
        path = attempts.begin(ledger, "bowl", dict(prompt="exact prompt"))
        attempts.fail(path, "Tool returned an error and no image")
        self.assertEqual(json.loads(path.read_text())["status"], "failed")
        attempts.begin(ledger, "bowl", dict(prompt="exact prompt"))
