"""V6.2 procedural roof contract regression tests."""
import importlib.util
import sys
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1] / "tools"
sys.path.insert(0, str(TOOLS))

def load(name):
    spec = importlib.util.spec_from_file_location(name, TOOLS / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

builder = load("v6_1_world_builder")
contract = load("v6_2_scene_contract")

class RoofTests(unittest.TestCase):
    def test_roof_data_is_deterministic(self):
        world = builder.build_world()
        a = contract.instances(world)
        self.assertEqual(a, contract.instances(world))
        self.assertGreater(len(a), 30)
        self.assertTrue(any(n["roof"]["style"] == "hip" for n in a))
        self.assertTrue(all(n["roof"]["height"] > 0 for n in a))
    def test_walls_and_gates_have_no_roof(self):
        instances = contract.instances(builder.build_world())
        self.assertTrue(all(n["roof"]["style"] == "none" for n in instances if n["kind"] in ("wall", "gate")))
    def test_roof_colors_are_hex(self):
        instances = contract.instances(builder.build_world())
        self.assertTrue(all(n["roof"]["color"].startswith("#") and len(n["roof"]["color"]) == 7 for n in instances))

if __name__ == "__main__":
    unittest.main()
