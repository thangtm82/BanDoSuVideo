"""Tests for deterministic world manifests and asset provenance."""
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1] / "tools"
sys.path.insert(0, str(TOOLS))
spec = importlib.util.spec_from_file_location("v6_1_world_builder", TOOLS / "v6_1_world_builder.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

class WorldBuilderTests(unittest.TestCase):
    def test_deterministic(self):
        a = module.build_world()
        self.assertEqual(a, module.build_world())
        self.assertGreaterEqual(len(a["nodes"]), 30)
        self.assertEqual(len(a["cameras"]), 3)
        self.assertEqual(len({n["id"] for n in a["nodes"]}), len(a["nodes"]))
        self.assertEqual(a["evidence_status"], "fictional_not_historically_verified")
    def test_epoch(self):
        early = module.build_world(epoch=1000)
        late = module.build_world(epoch=1200)
        self.assertLess(len(early["nodes"]), len(late["nodes"]))
        self.assertFalse(any(n["kind"] == "wall" for n in early["nodes"]))
        self.assertTrue(any(n["kind"] == "gate" for n in late["nodes"]))
    def test_asset_provenance(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "house.glb").write_bytes(b"glTF-test")
            manifest = module.build_world(catalog={
                "house": {"path": "house.glb", "license": "CC0-1.0", "source": "test fixture"}
            }, asset_root=root)
            self.assertEqual(len(manifest["asset_catalog"]["house"]["sha256"]), 64)
            self.assertTrue(any(n["asset_kind"] == "house" for n in manifest["nodes"]))
    def test_reject_traversal_and_missing_license(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "house.glb").write_bytes(b"fixture")
            with self.assertRaises(ValueError):
                module.validate_catalog({"house": {"path": "../house.glb", "license": "CC0", "source": "test"}}, root)
            with self.assertRaises(ValueError):
                module.validate_catalog({"house": {"path": "house.glb", "source": "test"}}, root)
    def test_invalid_seed(self):
        with self.assertRaises(ValueError):
            module.build_world(seed=True)

if __name__ == "__main__":
    unittest.main()
