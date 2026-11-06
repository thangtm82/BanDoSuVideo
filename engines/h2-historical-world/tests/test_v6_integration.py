"""Run: python -m unittest discover -s tests -p 'test_v6_integration.py'"""
import importlib.util
import unittest
from pathlib import Path

PATH = Path(__file__).resolve().parents[1] / "tools" / "v6_scene_plan.py"
spec = importlib.util.spec_from_file_location("v6_scene_plan", PATH)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

def sample():
    return {"version": "6.0", "worlds": [{"id": "w1"}], "scenes": [{"id": "s1", "type": "world3d", "world_id": "w1", "script_section_id": "p1", "start_seconds": 0, "end_seconds": 5, "camera": {"shot_size": "wide"}}]}

class ScenePlanTests(unittest.TestCase):
    def test_valid(self):
        self.assertEqual(module.validate(sample()), [])
    def test_missing_world(self):
        plan = sample()
        plan["scenes"][0]["world_id"] = "missing"
        self.assertTrue(module.validate(plan))
    def test_bad_timing(self):
        plan = sample()
        plan["scenes"][0]["end_seconds"] = 0
        self.assertTrue(module.validate(plan))
    def test_conversion(self):
        output = module.to_openmontage_scene_plan(sample())
        self.assertEqual(output["version"], "1.0")
        self.assertEqual(output["scenes"][0]["type"], "animation")
        self.assertEqual(output["metadata"]["h2_scene_refs"][0]["world_id"], "w1")

if __name__ == "__main__":
    unittest.main()
