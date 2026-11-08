"""Regression cases from the mask/icon/deployment failures, using isolated fixtures."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import artwork
import deploy
from PIL import Image, ImageDraw


class WorkflowTests(unittest.TestCase):
    def setUp(self):
        scratch = artwork.ROOT / "work"
        scratch.mkdir(exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="lumen-artwork-regression-", dir=scratch)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "source"
        self.destination = Path(self.temp.name) / "installed"
        for base in (self.root, self.destination):
            (base / "About").mkdir(parents=True)
            (base / "About/About.xml").write_text("<ModMetaData><packageId>fixture.lumen</packageId></ModMetaData>")
            (base / "Defs").mkdir()
        self.definition = self.root / "Defs/Bed.xml"
        self.definition.write_text("""<Defs><ThingDef><defName>Bed</defName><size>(1,2)</size>
          <graphicData><texPath>Things/Bed</texPath><graphicClass>Graphic_Multi</graphicClass>
          <drawSize>(2,2)</drawSize><shaderType>CutoutComplex</shaderType></graphicData>
          <defaultPlacingRot>South</defaultPlacingRot><uiIconScale>1</uiIconScale></ThingDef>
          <ResearchProjectDef><defName>Unlock</defName><techLevel>Neolithic</techLevel></ResearchProjectDef></Defs>""")
        self.installed = self.destination / "Defs/Bed.xml"
        self.installed.write_text(self.definition.read_text().replace("<uiIconScale>1", "<uiIconScale>0.5")
                                  .replace("Neolithic", "Medieval"))
        (self.root / "Textures/Things").mkdir(parents=True)
        for direction in artwork.DIRECTIONS:
            color = Image.new("RGBA", (64, 64))
            ImageDraw.Draw(color).rectangle((16, 2, 47, 61), fill=(200, 150, 160, 255))
            color.save(self.root / f"Textures/Things/Bed_{direction}.png")
            Image.new("RGBA", (64, 64), (255, 0, 0, 255)).save(
                self.root / f"Textures/Things/Bed_{direction}m.png")
        obj = {"id": "Bed", "label": "Bed", "texture": "Things/Bed", "canvas": [64, 64],
               "directions": list(artwork.DIRECTIONS), "maskEncoding": "opaque-red",
               "iconHeightFraction": [.7, .9],
               "contract": {k: "fixture invariant" for k in
                            ("identity", "geometry", "materials", "camera", "protected", "allowedEdits")},
               "approvedMaster": {"path": "Things/Bed_south.png", "approvedBy": "fixture",
                                  "approvedOn": "2026-10-29", "sha256": artwork.digest(
                                      self.root / "Textures/Things/Bed_south.png")}}
        (self.root / "artwork").mkdir()
        (self.root / "artwork/objects.json").write_text(json.dumps({"schema": 1, "objects": [obj],
            "renderer": {"sourceHashes": {}, "commandScale": .85, "modeledBleedRadius": 0},
            "materials": {"steel": [.4, .4, .4]}}))

    def check(self):
        return artwork.check(self.root)[0]

    def stage(self):
        return deploy.stage(self.root, self.destination, {"files": [{"path": "Defs/Bed.xml",
            "xmlSelectors": ["./ThingDef[defName='Bed']/uiIconScale"]}]}, self.root / "work/release")

    def linkRecipe(self):
        tools = self.root / "tools"
        tools.mkdir(exist_ok=True)
        shutil.copy2(artwork.ROOT / "tools/export-layered-artwork.py", tools / "export-layered-artwork.py")
        source = self.root / "artwork/source.png"
        shutil.copy2(self.root / "Textures/Things/Bed_north.png", source)
        manifest = dict(sourceSize=[64, 64], crop=[0, 0, 64, 64], canvas=[64, 64], targetBox=[0, 0, 64, 64],
                        sourceRepair="none", alphaHandling="preserve", colorFilename="color.png", maskFilename="mask.png",
                        layers=[dict(path="source.png", constructionTint=True)])
        (self.root / "artwork/master.json").write_text(json.dumps(manifest))
        import contextlib
        import io
        with contextlib.redirect_stdout(io.StringIO()):
            artwork.loadExporter(self.root).export(self.root / "artwork/master.json", self.root / "work/export")
        shutil.copy2(self.root / "work/export/mask.png", self.root / "Textures/Things/Bed_northm.png")
        registryPath = self.root / "artwork/objects.json"
        registry = json.loads(registryPath.read_text())
        registry["objects"][0]["recipes"] = {"north": "artwork/master.json"}
        registryPath.write_text(json.dumps(registry))

    def test_recipe_compares_pixels_and_rejects_edited_mask(self):
        self.linkRecipe()
        path = self.root / "Textures/Things/Bed_northm.png"
        with Image.open(path) as mask:
            mask.save(path, compress_level=0)
        errors, notes, *_ = artwork.check(self.root)
        self.assertEqual(errors, [])
        self.assertIn("Bed / north: recipe verified", notes)
        self.assertIn("Bed / south: no recipe", notes)
        with Image.open(path) as source:
            mask = source.copy()
        mask.putpixel((32, 32), (128, 0, 0, 255))
        mask.save(path)
        self.assertIn("Recipe mask differs", " ".join(self.check()))

    def test_valid_bed_and_full_sampling_preview(self):
        self.assertEqual(self.check(), [])
        errors, *_ = artwork.check(self.root, preview=self.root / "work/preview")
        self.assertEqual(errors, [])
        self.assertTrue((self.root / "work/preview/contact-sheet.png").is_file())

    def test_preview_keeps_protected_crystal_colors_with_bleed_model_enabled(self):
        color = Image.new("RGBA", (16, 16), (200, 150, 160, 255))
        mask = Image.new("RGBA", (16, 16), (0, 0, 0, 255))
        result = artwork.tinted(color, mask, (8, 8), (.4, .4, .4), 4)
        self.assertEqual(result.getpixel((4, 4)), (200, 150, 160, 255))

    def test_transparent_exclusion_is_rejected(self):
        path = self.root / "Textures/Things/Bed_northm.png"
        with Image.open(path) as source:
            mask = source.copy()
        mask.putpixel((32, 32), (0, 0, 0, 0))
        mask.save(path)
        self.assertIn("Transparent exclusion", " ".join(self.check()))

    def test_small_bed_icon_regression_is_rejected(self):
        self.definition.write_text(self.definition.read_text().replace("<uiIconScale>1", "<uiIconScale>0.5"))
        self.assertIn("outside approved range", " ".join(self.check()))

    def test_plain_icon_override_is_rejected(self):
        self.definition.write_text(self.definition.read_text().replace("<size>",
            "<uiIconPath>Things/Bed_south</uiIconPath><size>"))
        self.assertIn("bypasses the native UI material", " ".join(self.check()))

    def test_missing_direction_is_rejected(self):
        (self.root / "Textures/Things/Bed_east.png").unlink()
        self.assertIn("Bed_east.png", " ".join(self.check()))

    def test_mismatched_mask_is_rejected(self):
        Image.new("RGBA", (32, 32)).save(self.root / "Textures/Things/Bed_eastm.png")
        self.assertIn("dimensions or mode differ", " ".join(self.check()))

    def test_changed_master_is_rejected(self):
        Image.new("RGBA", (64, 64)).save(self.root / "Textures/Things/Bed_south.png")
        self.assertIn("Approved master changed", " ".join(self.check()))

    def test_xml_deployment_preserves_unrelated_research_and_verifies(self):
        manifest = self.stage()
        self.assertEqual(deploy.deploy(manifest), 1)
        doc = ET.parse(self.installed)
        self.assertEqual(doc.findtext("./ThingDef/uiIconScale"), "1")
        self.assertEqual(doc.findtext("./ResearchProjectDef/techLevel"), "Medieval")
        self.assertEqual(deploy.verify(manifest), 1)
        self.assertIn("<uiIconScale>0.5", (manifest.parent / "before/Defs/Bed.xml").read_text())

    def test_stale_installed_file_is_preserved(self):
        manifest = self.stage()
        self.installed.write_text(self.installed.read_text() + "\n<!-- another chat -->")
        before = self.installed.read_bytes()
        with self.assertRaisesRegex(ValueError, "Installed file changed"):
            deploy.deploy(manifest)
        self.assertEqual(self.installed.read_bytes(), before)

    def test_changed_candidate_is_rejected(self):
        manifest = self.stage()
        (manifest.parent / "candidate/Defs/Bed.xml").write_text("<changed/>")
        with self.assertRaisesRegex(ValueError, "Candidate changed"):
            deploy.deploy(manifest)

    def test_changed_source_is_rejected(self):
        manifest = self.stage()
        self.definition.write_text(self.definition.read_text() + "\n<!-- source edit -->")
        with self.assertRaisesRegex(ValueError, "Source changed"):
            deploy.deploy(manifest)

    def test_deployment_path_escape_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "outside root"):
            deploy.stage(self.root, self.destination, {"files": [{"path": "Defs/../../outside.xml"}]},
                         self.root / "work/release")

    def test_real_hook_uses_staged_content_and_rejects_staged_bad_mask(self):
        tools = self.root / "tools"
        self.linkRecipe()
        for module in ("check.py", "artwork.py", "artwork_family.py", "artwork_measure.py", "artwork_silhouettes.py"):
            shutil.copy2(artwork.ROOT / "tools" / module, tools / module)
        (tools / "tests").mkdir()
        hooks = self.root / ".githooks"
        hooks.mkdir()
        shutil.copy2(artwork.ROOT / ".githooks/pre-commit", hooks / "pre-commit")
        (hooks / "pre-commit").chmod(0o755)
        # Inside a pre-commit hook, GIT_INDEX_FILE and friends point at the outer commit; drop them so this
        # fixture repository doesn't write its entries into that index.
        env = {k: v for k, v in os.environ.items() if not k.startswith("GIT_")}
        def git(*args, check=True):
            return subprocess.run(["git", *args], cwd=self.root, capture_output=True, text=True, check=check, env=env)
        git("init", "-q")
        git("config", "core.hooksPath", ".githooks")
        git("config", "lumen.artworkPython", Path(sys.executable).as_posix())
        git("add", ".")
        path = self.root / "Textures/Things/Bed_northm.png"
        Image.new("RGBA", (64, 64)).save(path)
        result = git("hook", "run", "pre-commit", check=False)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        git("add", "Textures/Things/Bed_northm.png")
        result = git("hook", "run", "pre-commit", check=False)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Transparent exclusion", result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
