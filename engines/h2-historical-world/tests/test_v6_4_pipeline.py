"""V6.4 pipeline contract tests (no Blender required)."""
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools"))
import v6_4_pipeline as pipeline

class PipelineTests(unittest.TestCase):
    def test_missing_scene_fails(self):
        with tempfile.TemporaryDirectory() as d:
            with self.assertRaises(RuntimeError):
                pipeline.build(Path(d)/"missing.json",d)
    def test_invalid_camera_parameters(self):
        with tempfile.TemporaryDirectory() as d:
            with self.assertRaises(ValueError):
                pipeline.plan("scene.json",d,frames=1,dry_run=True)
    def test_dry_run_no_external_process(self):
        with tempfile.TemporaryDirectory() as d, patch.object(pipeline.subprocess,"run") as runner:
            natural=pipeline.build(__file__,d,dry_run=True)
            camera=pipeline.plan(natural,d,frames=48,dry_run=True)
            folder=pipeline.render(natural,camera,d,blender="blender",dry_run=True)
            pipeline.assemble(folder,d,dry_run=True)
            runner.assert_not_called()
    def test_prevent_mixed_frames(self):
        with tempfile.TemporaryDirectory() as d:
            folder=Path(d)/"frames"
            folder.mkdir()
            (folder/"frame_00001.png").write_bytes(b"old")
            with self.assertRaises(RuntimeError):
                pipeline.render("scene.json","camera.json",d,blender="blender",dry_run=True)
if __name__=="__main__":
    unittest.main()
