"""V6.2 Final contract and lighting regression tests (no Blender required)."""
import importlib.util
import json
import sys
import tempfile
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
validator = load("v6_2_validate_scene")
class FinalTests(unittest.TestCase):
    def test_generated_scene_passes_validation(self):
        with tempfile.TemporaryDirectory() as directory:
            world_file = Path(directory) / "world.json"
            scene_file = Path(directory) / "scene.json"
            world_file.write_text(json.dumps(builder.build_world()), encoding="utf-8")
            scene = contract.write_scene(world_file, scene_file)
            result = validator.validate(scene)
            self.assertGreater(result["instances"], 0)
            self.assertGreater(result["details"], 0)
    def test_stale_scene_rejected(self):
        with self.assertRaisesRegex(ValueError, "procedural details"):
            validator.validate({"schema": "h2.historical_world.preview.v6.2",
                                "instances": [{"id": "a"}], "details": []})
    def test_duplicate_id_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            world_file = Path(directory) / "world.json"
            scene_file = Path(directory) / "scene.json"
            world_file.write_text(json.dumps(builder.build_world()), encoding="utf-8")
            scene = contract.write_scene(world_file, scene_file)
            scene["details"][0]["id"] = scene["instances"][0]["id"]
            with self.assertRaisesRegex(ValueError, "duplicate"):
                validator.validate(scene)
    def test_final_lighting_preset_present(self):
        source = (TOOLS / "v6_2_blender_preview.py").read_text(encoding="utf-8")
        self.assertIn('"bright_final":', source)
        self.assertIn('"world": 0.55, "sun": 3.0, "fill": 12000', source)
if __name__ == "__main__":
    unittest.main()
