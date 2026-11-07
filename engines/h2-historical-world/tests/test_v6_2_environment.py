"""Shared environment data tests; no graphics runtime needed."""
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
class EnvironmentTests(unittest.TestCase):
    def test_deterministic_environment(self):
        world=builder.build_world()
        a=contract.environment(world)
        self.assertEqual(a,contract.environment(world))
        self.assertGreater(len(a["roads"]),0)
        self.assertGreaterEqual(len(a["river"]["points"]),2)
        self.assertGreater(a["river"]["width"],0)
        self.assertGreater(a["lighting"]["sun_energy"],0)
    def test_roof_and_scene_contract(self):
        world=builder.build_world()
        self.assertTrue(any(i["roof"]["style"]=="hip" for i in contract.instances(world)))
        self.assertEqual(contract.environment(world)["lighting"]["exposure"],0.0)
if __name__=="__main__":
    unittest.main()
