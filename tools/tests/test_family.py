"""Family-rule tooling: measurements, silhouette sheets, measured checks and weapon rotation."""
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import artwork
import artwork_family
import artwork_measure
import artwork_silhouettes
import rotate_weapon
from PIL import Image, ImageDraw


def sprite(path, outline, fill, size=64):
    image = Image.new("RGBA", (size, size))
    draw = ImageDraw.Draw(image)
    draw.rectangle((12, 8, 51, 55), fill=(10, 10, 10, 255))
    draw.rectangle((12 + outline, 8 + outline, 51 - outline, 55 - outline), fill=fill)
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)


class FamilyTests(unittest.TestCase):
    def setUp(self):
        scratch = artwork.ROOT / "work"
        scratch.mkdir(exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="lumen-family-", dir=scratch)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def stones(self, serenite, other, hue=(200, 80, 200)):
        for stone, outline, fill in (("Serenite", serenite, (200, 80, 200, 255)), ("Verdite", other, hue + (255,))):
            sprite(self.root / f"Textures/Things/Building/{stone}/Pylon.png", outline, fill)
            sprite(self.root / f"Textures/Things/Item/Resource/{stone}/{stone}_c.png", outline, fill)

    def config(self, **exceptions):
        (self.root / "artwork").mkdir(exist_ok=True)
        (self.root / artwork_family.CONFIG).write_text(json.dumps(
            {"outlineTolerancePx": 1.0, "minHueGapDegrees": 20, "exceptions": exceptions}))

    def test_outline_weight_is_measured_at_128px(self):
        thin, thick = (self.root / "thin.png"), (self.root / "thick.png")
        sprite(thin, 1, (230, 230, 230, 255), 128)
        sprite(thick, 4, (230, 230, 230, 255), 128)
        self.assertLess(artwork_measure.measure(thin, False)["outlinePx"],
                        artwork_measure.measure(thick, False)["outlinePx"])

    def test_outline_and_hue_failures_need_an_exception(self):
        self.stones(1, 5, hue=(210, 90, 210))
        self.config()
        errors, _ = artwork_family.familyChecks(self.root)
        self.assertTrue(any(e.startswith("outline:Verdite Pylon") for e in errors))
        self.assertTrue(any(e.startswith("hue:Serenite/Verdite") for e in errors))

    def test_exceptions_suppress_findings_without_requiring_stale_cleanup(self):
        self.stones(1, 5, hue=(210, 90, 210))
        self.config(**{"outline:Verdite Pylon": "t", "outline:Verdite stack of three": "t",
                       "hue:Serenite/Verdite": "t", "outline:Gone": "stale"})
        errors, _ = artwork_family.familyChecks(self.root)
        self.assertEqual(errors, [])

    def test_silhouette_sheet_is_written_per_fixture_type(self):
        self.stones(1, 1)
        written = artwork_silhouettes.render(self.root, self.root / "out")
        self.assertEqual({p.name for p in written}, {"silhouettes-Pylon.png", "silhouettes-Stack.png"})

    def test_rotation_puts_the_head_to_the_right(self):
        image = Image.new("RGBA", (128, 128))
        draw = ImageDraw.Draw(image)
        draw.line((20, 108, 90, 38), fill=(40, 40, 40, 255), width=6)
        draw.polygon([(80, 20), (110, 50), (96, 56), (70, 30)], fill=(220, 160, 60, 255))
        angle = rotate_weapon.headAngle(image)
        self.assertTrue(30 < angle < 60, angle)
        turned = rotate_weapon.rotate(image, -angle)
        box = turned.getchannel("A").getbbox()
        self.assertGreater(box[2] - box[0], box[3] - box[1])
        self.assertEqual(turned.width, turned.height)


if __name__ == "__main__":
    unittest.main()
