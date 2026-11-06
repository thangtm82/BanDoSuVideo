"""Contract tests run without Blender or Three.js."""
import importlib.util
import json
import sys
import tempfile
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
class ContractTests(unittest.TestCase):
    def test_all_instances_and_coordinates(self):
        world=builder.build_world()
        result=contract.instances(world)
        self.assertEqual(len(result),len(world["nodes"]))
        for node,instance in zip(world["nodes"],result):
            self.assertEqual(node["id"],instance["id"])
            self.assertEqual(node["position"][0],instance["position"][0])
            self.assertEqual(node["position"][2],instance["position"][2])
            self.assertGreater(instance["position"][1],0)
    def test_reject_duplicate_ids(self):
        world=builder.build_world()
        world["nodes"].append(dict(world["nodes"][0]))
        with self.assertRaisesRegex(ValueError,"Duplicate"):
            contract.instances(world)
    def test_reject_nonfinite_position(self):
        world=builder.build_world()
        world["nodes"][0]["position"][0]=float("nan")
        with self.assertRaisesRegex(ValueError,"Invalid position"):
            contract.instances(world)
    def test_scene_round_trip(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp)
            src=root/"world.json";dest=root/"scene.json"
            src.write_text(json.dumps(builder.build_world()),encoding="utf-8")
            scene=contract.write_scene(src,dest)
            self.assertEqual(scene,json.loads(dest.read_text(encoding="utf-8")))
            self.assertEqual(len(scene["cameras"]),3)
            self.assertEqual(scene["schema"],"h2.historical_world.preview.v6.2")
if __name__=="__main__":
    unittest.main()
