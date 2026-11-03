import sys
import unittest
from pathlib import Path

import numpy as np

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import crystal_light as light


class CrystalLightTests(unittest.TestCase):
    def weight(self, name):
        spec = light.OBJECTS[name]
        colour, tint = light.loadSprite(light.ROOT / spec["colour"])
        return colour, tint, light.crystalWeight(colour, tint, spec["cut"], exclude=spec.get("exclude", ()))

    def test_pattern_loops_exactly(self):
        for kind in ("bands", "flow"):
            first = light.pattern(kind, (64, 48), 0.0)
            wrapped = light.pattern(kind, (64, 48), 1.0)
            self.assertTrue(np.allclose(first, wrapped), kind)

    def test_pattern_is_banded(self):
        values = np.unique(light.pattern("flow", (128, 128), 0.3))
        self.assertLessEqual(len(values), light.LEVELS)

    def test_frames_are_deterministic(self):
        _, _, weight = self.weight("vitalite-pylon")
        a = light.frames("bands", weight, 8)
        b = light.frames("bands", weight, 8)
        self.assertTrue(all(np.array_equal(x, y) for x, y in zip(a, b)))

    def test_frames_stay_inside_crystal(self):
        for name in light.OBJECTS:
            colour, tint, weight = self.weight(name)
            for alpha in light.frames(light.OBJECTS[name]["kind"], weight, 4):
                self.assertTrue(np.all(alpha[weight == 0] == 0), name)
                self.assertTrue(np.all(alpha[colour[..., 3] == 0] == 0), name)

    def test_housing_and_cut_are_excluded(self):
        colour, tint, weight = self.weight("sunstone-obelisk")
        housing = (tint[..., 0] * tint[..., 3]) > 0.5
        self.assertEqual(float(weight[housing].max()), 0.0)
        self.assertEqual(float(weight[light.OBJECTS["sunstone-obelisk"]["cut"]:].max()), 0.0)

    def test_reverse_flow_runs_the_other_way(self):
        _, _, weight = self.weight("sunstone-obelisk")
        up = light.frames("flow", weight, 8)
        down = light.frames("flow", weight, 8, reverse=True)
        self.assertTrue(np.array_equal(up[0], down[0]))
        self.assertTrue(np.array_equal(up[1], down[7]))


if __name__ == "__main__":
    unittest.main()
