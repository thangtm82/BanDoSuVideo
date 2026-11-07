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
        self.assertEqual(len(a["details"]),0)
        # First triangle must face upward in canonical Y-up coordinates.
        v=terrain["vertices"];i,j,k=terrain["triangles"][0]
        ab=[v[j][n]-v[i][n] for n in range(3)]
        ac=[v[k][n]-v[i][n] for n in range(3)]
        self.assertGreater(ab[2]*ac[0]-ab[0]*ac[2],0)
    def test_bridge_at_real_road_crossing(self):
        scene=self.scene()
        scene["environment"]["roads"]=[{"id":"main","points":[[0,0,-50],[0,0,50]],"width":5}]
        upgraded=natural.upgrade(scene,resolution=9)
        self.assertEqual(len(upgraded["environment"]["bridge_crossings"]),1)
        self.assertEqual(upgraded["details"][0]["id"],"v6_3_bridge_0")
        self.assertAlmostEqual(upgraded["details"][0]["position"][0],0)
        self.assertAlmostEqual(upgraded["details"][0]["position"][2],0)
        self.assertEqual(len([d for d in upgraded["details"] if "parapet" in d["id"]]),2)
        self.assertEqual(len([d for d in upgraded["details"] if "-post-" in d["id"]]),4)
        self.assertEqual(len([d for d in upgraded["details"] if "-abutment-" in d["id"]]),2)
    def test_no_decorative_bridge_without_road(self):
        self.assertFalse(natural.upgrade(self.scene(),resolution=9)["environment"]["bridge_crossings"])
    def test_watergate_cuts_wall_geometry(self):
        scene=self.scene()
        scene["instances"]=[{"id":"wall-cross","kind":"wall",
            "position":[0,6,0],"dimensions":[100,12,5],
            "rotation_y":0.0,"color":"#a99a83","roof":{"style":"none"}}]
        scene["details"]=[{"id":"wall-cross-merlon-0","shape":"box",
            "position":[0,13,0],"size":[3,2,3],"color":"#a99a83"}]
        scene["environment"]["river"]["points"]=[[0,0,-100],[0,0,100]]
        upgraded=natural.upgrade(scene,resolution=9)
        self.assertEqual(upgraded["environment"]["watergate_cut_wall_ids"],["wall-cross"])
        self.assertFalse(upgraded["environment"]["river_wall_intersections_unresolved"])
        self.assertEqual(len(upgraded["instances"]),2)
        self.assertFalse(any(d["id"]=="wall-cross-merlon-0" for d in upgraded["details"]))
        self.assertAlmostEqual(sum(w["dimensions"][0] for w in upgraded["instances"]),80.0)
    def test_gate_crossing_removes_fake_arch_decal(self):
        scene=self.scene()
        scene["instances"]=[{"id":"gate-water","kind":"gate",
            "position":[0,9,0],"dimensions":[80,18,9],
            "rotation_y":0.0,"color":"#b8a78c","roof":{"style":"none"}}]
        scene["details"]=[{"id":"gate-water-arch-dark","shape":"box",
            "position":[0,4,4.5],"size":[12,8,.35],"color":"#514c42"}]
        scene["environment"]["river"]["points"]=[[0,0,-100],[0,0,100]]
        upgraded=natural.upgrade(scene,resolution=9)
        self.assertEqual(len(upgraded["instances"]),2)
        self.assertNotIn("gate-water-arch-dark",[d["id"] for d in upgraded["details"]])
        self.assertTrue(upgraded["environment"]["watergate_review_required"])
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
    def test_cinematic_phases_and_dolly(self):
        plan=cinema.camera_path(self.scene(),frames=120,fps=24)
        shots=plan["shots"]
        self.assertEqual({s["phase"] for s in shots},
                         {"establishing","approach","flyby"})
        self.assertLess(math.dist(shots[-1]["position"],shots[-1]["target"]),
                        math.dist(shots[0]["position"],shots[0]["target"]))
        self.assertTrue(all(shots[i]["frame"] == i+1 for i in range(120)))
    def test_camera_targets_landmark(self):
        scene=self.scene()
        scene["instances"]=[{"id":"temple","kind":"temple",
            "position":[35,10,-20],"dimensions":[30,30,30]}]
        shots=cinema.camera_path(scene,frames=120)["shots"]
        self.assertEqual(shots[0]["target"],[35,17.5,-20])
        self.assertGreaterEqual(min(s["position"][1] for s in shots),52.5)
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
