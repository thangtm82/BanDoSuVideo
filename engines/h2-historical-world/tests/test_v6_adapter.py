"""Regression tests for V6 cross-engine contract and Blender artifact gate."""
import importlib.util
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace

TOOLS = Path(__file__).resolve().parents[1] / "tools"

def load(name):
    spec = importlib.util.spec_from_file_location(name, TOOLS / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

adapter = load("v6_world_spec_adapter")
bridge = load("v6_openmontage_bridge")

class AdapterTests(unittest.TestCase):
    def test_hex_color(self):
        self.assertEqual(adapter.rgba("#ff0000"), [1.0, 0.0, 0.0, 1.0])
    def test_invalid_color(self):
        with self.assertRaises(ValueError):
            adapter.rgba("not-a-color")
    def test_coordinates_and_region(self):
        result = adapter.convert({"seed": 42, "world": {"size": 120},
            "regions": [{"id": "city", "center": [0.5, -0.5], "radius": 0.5, "color": "#808080"}],
            "camera_path": [{"time": 0, "position": [10, 20, 30], "target": [0, 0, 0]}]})
        self.assertEqual(result["regions"][0]["center"], [30.0, -30.0])
        self.assertEqual(result["camera"]["path"][0]["position"], [10.0, 30.0, 20.0])
        self.assertEqual(result["assets"], [])
    def test_no_world_size_zero(self):
        with self.assertRaises(ValueError):
            adapter.convert({"world": {"size": 0}})

class ArtifactTests(unittest.TestCase):
    def test_missing_blend_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            payload = {"operation": "build", "blend_path": str(Path(d) / "missing.blend")}
            result = SimpleNamespace(success=True)
            self.assertIn("missing", bridge.verify_artifacts("blender_world", payload, result))
    def test_existing_blend_accepted(self):
        with tempfile.TemporaryDirectory() as d:
            blend = Path(d) / "world.blend"
            blend.write_bytes(b"BLENDER-test")
            result = SimpleNamespace(success=True)
            self.assertIsNone(bridge.verify_artifacts("blender_world", {"operation": "build", "blend_path": str(blend)}, result))
    def test_missing_png_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            blend = Path(d) / "world.blend"
            blend.write_bytes(b"BLENDER-test")
            payload = {"operation": "render_still", "blend_path": str(blend), "output_path": str(Path(d) / "preview.png")}
            self.assertIn("preview.png", bridge.verify_artifacts("blender_world", payload, SimpleNamespace(success=True)))

if __name__ == "__main__":
    unittest.main()
