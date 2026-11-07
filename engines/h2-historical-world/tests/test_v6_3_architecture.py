"""V6.3 Final-B architecture regression tests."""
import copy
import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"tools"))
import v6_3_scene_upgrade as architecture

def item(kind,name):
    return {"id":name,"kind":kind,"position":[0,10,0],
            "dimensions":[20,20,20],"rotation_y":0}

class ArchitectureTests(unittest.TestCase):
    def test_landmark_details(self):
        scene={"schema":"h2.historical_world.preview.v6.2",
               "instances":[item("temple","temple-a")],"details":[]}
        original=copy.deepcopy(scene)
        upgraded=architecture.upgrade(scene)
        ids={d["id"] for d in upgraded["details"]}
        for suffix in ("-stair-0","-stair-1","-stair-2",
                       "-portico-pillar--1","-portico-pillar-1",
                       "-portico-lintel","-roof-tier"):
            self.assertIn("temple-a"+suffix,ids)
        self.assertEqual(scene,original)
        self.assertEqual(upgraded,architecture.upgrade(scene))
    def test_house_facades(self):
        scene={"schema":"h2.historical_world.preview.v6.2",
               "instances":[item("house","house-"+str(i)) for i in range(8)],
               "details":[]}
        upgraded=architecture.upgrade(scene)
        ids=[d["id"] for d in upgraded["details"]]
        self.assertEqual(len(ids),len(set(ids)))
        self.assertTrue(any("-facade-band" in s for s in ids))
        self.assertTrue(any("-front-stoop" in s for s in ids))
        self.assertTrue(any("-upper-window" in s for s in ids))
        self.assertTrue(any("-roof-crest" in s for s in ids))
if __name__=="__main__":
    unittest.main()
