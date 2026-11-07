"""V6.3b/c regression tests; no Blender or FFmpeg required."""
import copy
import math
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools"))
import v6_3_natural_world as natural
import v6_3_cinematic as cinema

class WorldTests(unittest.TestCase):
    def scene(self):
        return {"schema":"h2.historical_world.preview.v6.2",
                "instances":[],"details":[],"environment":{
                "river":{"points":[[-100,0,0],[0,0,0],[100,0,0]],"width":14},
                "roads":[]},
                "cameras":[{"position":[520,260,500],"target":[0,0,0],"fov":45}]}
    def test_terrain_deterministic(self):
        source=self.scene()
        original=copy.deepcopy(source)
        a=natural.upgrade(source,resolution=9)
        self.assertEqual(a,natural.upgrade(source,resolution=9))
        self.assertEqual(source,original)
        terrain=a["environment"]["terrain_mesh"]
        self.assertEqual(len(terrain["vertices"]),81)
        self.assertEqual(len(terrain["triangles"]),128)
        self.assertEqual(len(a["details"]),1)
        # First triangle must face upward in canonical Y-up coordinates.
        v=terrain["vertices"];i,j,k=terrain["triangles"][0]
        ab=[v[j][n]-v[i][n] for n in range(3)]
        ac=[v[k][n]-v[i][n] for n in range(3)]
        self.assertGreater(ab[2]*ac[0]-ab[0]*ac[2],0)
    def test_bad_inputs(self):
        with self.assertRaises(ValueError): natural.upgrade(self.scene(),resolution=10)
        scene=self.scene();scene["environment"]["river"]["points"]=[]
        with self.assertRaises(ValueError): natural.upgrade(scene)
    def test_camera_path(self):
        plan=cinema.camera_path(self.scene(),frames=5,fps=25)
        self.assertEqual(len(plan["shots"]),5)
        self.assertEqual(plan["shots"][0]["frame"],1)
        self.assertEqual(plan["shots"][-1]["frame"],5)
        self.assertEqual(plan,cinema.camera_path(self.scene(),frames=5,fps=25))
    def test_assemble_requires_frames(self):
        with tempfile.TemporaryDirectory() as d:
            with self.assertRaises(FileNotFoundError):
                cinema.assemble(d,str(Path(d)/"video.mp4"))
    def test_assemble_rejects_gaps(self):
        with tempfile.TemporaryDirectory() as d:
            Path(d,"frame_00001.png").write_bytes(b"fake")
            Path(d,"frame_00003.png").write_bytes(b"fake")
            with self.assertRaises(ValueError):
                cinema.assemble(d,str(Path(d)/"video.mp4"))
if __name__=="__main__":
    unittest.main()
