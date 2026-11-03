import contextlib
import importlib.util
import io
import json
import shutil
import sys
import subprocess
from pathlib import Path
import tempfile
import unittest

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
import artwork_candidate

spec = importlib.util.spec_from_file_location("layered_export", ROOT / "tools/export-layered-artwork.py")
exporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(exporter)


class OwnershipTests(unittest.TestCase):
    def test_validation(self):
        for entry in ({}, {"constructionTint": True, "ownership": {}},
                      {"ownership": {"bad": [[0, 0], [1, 1], [2, 2]]}},
                      {"ownership": {"bad": [[0, 0], [8, 0], [0, 1]]}},
                      {"ownership": {"bad": [[0, 0], [1, 0]]}},
                      {"ownership": {"a": [[0, 0], [7, 0], [0, 7]]}, "scale": .5}):
            with self.subTest(entry=entry), self.assertRaises(ValueError):
                exporter.ownershipMap(entry, (8, 8))

    def test_export_identity_and_occlusion(self):
        with tempfile.TemporaryDirectory(dir=ROOT / "work") as directory:
            root = Path(directory)
            Image.new("RGBA", (8, 8), (180, 180, 180, 255)).save(root / "base.png")
            Image.new("RGBA", (2, 2), (255, 0, 0, 128)).save(root / "fixed.png")
            manifest = dict(sourceSize=[8, 8], crop=[0, 0, 8, 8], canvas=[8, 8], targetBox=[0, 0, 8, 8],
                            sourceRepair="none", alphaHandling="preserve", colorFilename="color.png", maskFilename="mask.png",
                            layers=[dict(path="base.png", constructionTint=True), dict(path="fixed.png", constructionTint=False, offset=[2, 2])])
            path = root / "master.json"
            def export(name):
                path.write_text(json.dumps(manifest))
                with contextlib.redirect_stdout(io.StringIO()):
                    exporter.export(path, root / name)
            export("legacy")
            manifest["layers"][0] = dict(path="base.png", ownership={"all": [[0, 0], [7, 0], [7, 7], [0, 7]]})
            export("owned")
            for name in ("color.png", "mask.png"):
                self.assertEqual((root / "legacy" / name).read_bytes(), (root / "owned" / name).read_bytes())
            with Image.open(root / "owned/mask.png") as mask:
                self.assertEqual(mask.getpixel((2, 2)), (127, 0, 0, 255))
            manifest["layers"][0]["ownership"] = {"part": [[0, 0], [3, 0], [3, 7], [0, 7]]}
            export("partial")
            self.assertEqual((root / "legacy/color.png").read_bytes(), (root / "partial/color.png").read_bytes())
            with Image.open(root / "partial/mask.png") as mask:
                self.assertEqual(mask.getpixel((1, 1))[0], 255)
                self.assertEqual(mask.getpixel((6, 1))[0], 0)

    def test_dreadstone_rough_boundaries_warn(self):
        source = ROOT / "artwork/design-sources/dreadstone-column"
        with tempfile.TemporaryDirectory(dir=ROOT / "work") as directory:
            root = Path(directory)
            shutil.copyfile(source / "source.png", root / "source.png")
            manifest = json.loads((source / "master.json").read_text())
            manifest["layers"][0]["ownership"] = {
                "cap": [[340,40],[920,40],[920,254],[340,254]],
                "collar": [[380,560],[470,580],[620,592],[720,592],[780,580],[880,560],[880,700],[780,722],[720,720],[620,720],[470,716],[380,700]],
                "plinth": [[340,990],[470,1013],[560,1033],[620,1035],[720,1035],[780,1015],[920,990],[920,1200],[340,1200]]}
            path = root / "master.json"
            path.write_text(json.dumps(manifest))
            with contextlib.redirect_stdout(io.StringIO()):
                exporter.export(path, root / "out")
            report = artwork_candidate.checkCandidate(ROOT, path, root / "out")
            self.assertGreater(report["boundaryDiagnostics"]["brightNeutralWithoutTint"], 0)
            self.assertEqual(report["boundaryDiagnostics"]["strongColorWithTint"], 0)
            with Image.open(root / "out/DreadstoneColumn.png") as color, Image.open(root / "out/DreadstoneColumn_m.png") as mask:
                for lo, hi in ((220, 310), (410, 470)):
                    hits = sum(1 for y in range(lo, hi) for x in range(512)
                               if (lambda p, q: p[3] > 8 and min(p[:3]) >= 180 and max(p[:3])-min(p[:3]) <= 20 and q[0] < 32)(color.getpixel((x,y)), mask.getpixel((x,y))))
                    self.assertGreater(hits, 0)

    def test_dreadstone_candidate_samples_and_color_identity(self):
        source = ROOT / "artwork/design-sources/dreadstone-column"
        with tempfile.TemporaryDirectory(dir=ROOT / "work") as directory:
            root = Path(directory)
            shutil.copyfile(source / "source.png", root / "source.png")
            manifest = json.loads((source / "master.json").read_text())
            path = root / "master.json"
            path.write_text(json.dumps(manifest))
            with contextlib.redirect_stdout(io.StringIO()):
                exporter.export(path, root / "owned")
                report = artwork_candidate.checkCandidate(ROOT, path, root / "owned")
                manifest["layers"] = [{"path": "source.png", "constructionTint": False}]
                path.write_text(json.dumps(manifest))
                exporter.export(path, root / "fixed")
            self.assertTrue(report["readyForTintReview"])
            self.assertEqual((root / "owned/DreadstoneColumn.png").read_bytes(), (root / "fixed/DreadstoneColumn.png").read_bytes())
            with Image.open(root / "owned/DreadstoneColumn_m.png") as mask:
                for point in ((255, 90), (255, 300), (255, 470)):
                    self.assertEqual(mask.getpixel(point)[0], 255)
                for point in ((220, 180), (265, 180), (260, 390)):
                    self.assertEqual(mask.getpixel(point)[0], 0)

    def test_prepare_command_failure_evidence_and_path_limit(self):
        with tempfile.TemporaryDirectory(dir=ROOT / "work") as directory:
            root = Path(directory)
            Image.new("RGBA", (8, 8), (180, 180, 180, 255)).save(root / "source.png")
            manifest = dict(sourceSize=[8, 8], crop=[0, 0, 8, 8], canvas=[8, 8], targetBox=[0, 0, 8, 8],
                            sourceRepair="none", alphaHandling="preserve", colorFilename="color.png", maskFilename="mask.png",
                            layers=[dict(path="source.png", constructionTint=True)],
                            candidateChecks=dict(mode="new-design", expectedBounds=[0,0,8,8], footLine=8,
                                                 boundsTolerance=0, minSourceSize=8, minSourcePixelsPerOutputPixel=1,
                                                 maxOpaqueSpread=8))
            manifest["reviewJoins"] = {"front-lip": [1, 1, 7, 7]}
            path = root / "master.json"
            def run(output):
                path.write_text(json.dumps(manifest))
                return subprocess.run([sys.executable, str(ROOT / "tools/artwork.py"), "prepare-candidate",
                                       str(path), "--output", str(output)], capture_output=True, text=True)
            output = root / "out"
            self.assertEqual(run(output).returncode, 0)
            self.assertTrue((output / "cpu-dark.png").is_file())
            report = json.loads((output / "candidate-check.json").read_text())
            self.assertEqual(len(report["joinViews"]), 3)
            for tint in ("neutral", "brown", "dark"):
                self.assertTrue((output / f"cpu-{tint}-front-lip-joins.png").is_file())
            manifest["reviewJoins"]["front-lip"] = [1, 1, 9, 7]
            result = run(output)
            self.assertEqual(result.returncode, 1)
            self.assertIn("Invalid review join rectangle", result.stderr)
            manifest["reviewJoins"]["front-lip"] = [1, 1, 7, 7]
            manifest["candidateChecks"]["maxOpaqueSpread"] = -1
            result = run(output)
            self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
            self.assertFalse(json.loads((output / "candidate-check.json").read_text())["passed"])
            self.assertTrue((output / "color.png").is_file())
            self.assertEqual(run(ROOT / "outside-work-review").returncode, 1)
            self.assertFalse((ROOT / "outside-work-review").exists())
