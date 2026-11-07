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
    def test_progress_event_contract(self):
        from io import StringIO
        with patch("sys.stdout", new_callable=StringIO) as stream:
            pipeline.emit("camera", "completed", frames=48)
        line=stream.getvalue().strip()
        self.assertTrue(line.startswith("H2_PROGRESS "))
        import json
        event=json.loads(line[len("H2_PROGRESS "):])
        self.assertEqual(event["stage"],"camera")
        self.assertEqual(event["status"],"completed")
        self.assertEqual(event["frames"],48)
    def test_clean_frames_is_opt_in(self):
        with tempfile.TemporaryDirectory() as d, patch.object(pipeline.subprocess,"run") as runner:
            folder=Path(d)/"frames"
            folder.mkdir()
            stale=folder/"frame_00001.png"
            stale.write_bytes(b"old")
            camera=Path(d)/"camera.json"
            camera.write_text('{"frames":2}')
            natural=Path(d)/"scene.json"
            natural.write_text("{}")
            def make_frames(*args,**kwargs):
                (folder/"frame_00001.png").write_bytes(b"new")
                (folder/"frame_00002.png").write_bytes(b"new")
            runner.side_effect=make_frames
            result=pipeline.render(natural,camera,d,blender="blender",clean_frames=True)
            self.assertEqual(result,folder)
            self.assertEqual(stale.read_bytes(),b"new")
    def test_prevent_mixed_frames(self):
        with tempfile.TemporaryDirectory() as d:
            folder=Path(d)/"frames"
            folder.mkdir()
            (folder/"frame_00001.png").write_bytes(b"old")
            with self.assertRaises(RuntimeError):
                pipeline.render("scene.json","camera.json",d,blender="blender",dry_run=True)
if __name__=="__main__":
    unittest.main()
