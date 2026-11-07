"""V6.3a upgrade contract tests; runs without Blender."""
import copy
import importlib.util
import sys
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1] / "tools"
sys.path.insert(0, str(TOOLS))
import v6_3_scene_upgrade as upgrade

class UpgradeTests(unittest.TestCase):
    def scene(self):
        def obj(name, kind):
            return {"id": name, "kind": kind, "position": [0, 6, 0],
                    "dimensions": [12, 12, 12], "rotation_y": 0.0}
        return {"schema": "h2.historical_world.preview.v6.2",
                "instances": [obj("house-1", "house"), obj("market-1", "market"),
                              obj("temple-1", "temple"), obj("tower-1", "tower")],
                "details": [{"id": "existing", "shape": "box"}]}
    def test_deterministic_and_preserves_input(self):
        source = self.scene()
        original = copy.deepcopy(source)
        a = upgrade.upgrade(source)
        self.assertEqual(a, upgrade.upgrade(source))
        self.assertEqual(source, original)
        self.assertGreater(len(a["details"]), len(source["details"]))
        self.assertEqual(len({o["id"] for o in a["details"]}),
                         len(a["details"]))
    def test_facade_and_monument(self):
        ids = [o["id"] for o in upgrade.upgrade(self.scene())["details"]]
        for expected in ("house-1-door", "market-1-awning",
                         "temple-1-column--1", "tower-1-slit-1"):
            self.assertIn(expected, ids)
    def test_rejects_wrong_schema(self):
        with self.assertRaises(ValueError):
            upgrade.upgrade({"schema": "invalid"})
    def test_duplicate_generated_id(self):
        scene = self.scene()
        scene["details"].append({"id": "house-1-door"})
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            upgrade.upgrade(scene)

if __name__ == "__main__":
    unittest.main()
