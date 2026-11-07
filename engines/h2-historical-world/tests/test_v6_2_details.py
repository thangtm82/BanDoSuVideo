"""V6.2 enhanced shared architecture/environment tests."""
import importlib.util
import sys
import unittest
from pathlib import Path
TOOLS=Path(__file__).resolve().parents[1]/"tools"
sys.path.insert(0,str(TOOLS))
def load(name):
    spec=importlib.util.spec_from_file_location(name,TOOLS/(name+".py"))
    mod=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod
builder=load("v6_1_world_builder")
contract=load("v6_2_scene_contract")
class DetailsTests(unittest.TestCase):
    def test_details_deterministic_and_unique(self):
        world=builder.build_world()
        a=contract.architectural_details(world)
        self.assertEqual(a,contract.architectural_details(world))
        self.assertEqual(len(a),len({x["id"] for x in a}))
        self.assertGreater(len(a),100)
    def test_architecture_and_landscape(self):
        data=contract.architectural_details(builder.build_world())
        ids=[d["id"] for d in data]
        self.assertTrue(any("merlon" in x for x in ids))
        self.assertTrue(any("arch-dark" in x for x in ids))
        self.assertTrue(any(x.startswith("tree-") for x in ids))
        self.assertEqual(sum(x.startswith("hill-") for x in ids),5)
        self.assertTrue(all(x["shape"] in ("box","cone") for x in data))
    def test_lighting_and_ribbons(self):
        env=contract.environment(builder.build_world())
        self.assertGreater(env["lighting"]["sun_energy"],0)
        self.assertGreater(env["lighting"]["ambient_strength"],0)
        self.assertTrue(all(r["width"]>0 for r in env["roads"]))
        self.assertGreater(env["river"]["width"],0)
if __name__=="__main__":
    unittest.main()
