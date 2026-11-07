"""V6.3b shared deterministic terrain, river banks and bridges (Y-up metres).

Terrain is a regular grid. A river depression is applied to vertices; river
surface follows the same path. Roads remain overlays at town elevation.
"""
import argparse
import json
import math
from pathlib import Path

def upgrade(scene, resolution=49, extent=600):
    if scene.get("schema") != "h2.historical_world.preview.v6.2":
        raise ValueError("Expected V6.2-compatible scene")
    if not 9 <= resolution <= 129 or resolution % 2 != 1:
        raise ValueError("resolution must be odd, between 9 and 129")
    result = json.loads(json.dumps(scene))
    env = result.setdefault("environment", {})
    river = env.get("river", {})
    points = river.get("points", [])
    if len(points) < 2:
        raise ValueError("A river polyline is required")
    width = float(river.get("width", 14))
    if width <= 0:
        raise ValueError("River width must be positive")
    def distance_segment(x,z,a,b):
        vx, vz = b[0]-a[0], b[2]-a[2]
        t = max(0.,min(1.,((x-a[0])*vx+(z-a[2])*vz)/(vx*vx+vz*vz or 1)))
        return math.hypot(x-a[0]-t*vx,z-a[2]-t*vz)
    def dist_river(x,z):
        return min(distance_segment(x,z,a,b) for a,b in zip(points,points[1:]))
    vertices=[]
    for j in range(resolution):
        z = -extent + 2*extent*j/(resolution-1)
        for i in range(resolution):
            x = -extent + 2*extent*i/(resolution-1)
            # Preserve the level town center; hills blend in outside its walls.
            edge = max(0.,min(1.,(max(abs(x),abs(z))-290)/200))
            edge = edge*edge*(3-2*edge)
            elevation = edge*(22*math.sin(x/95)*math.cos(z/113)+8*math.sin(z/51))
            distance = dist_river(x,z)
            if distance < width*1.3:
                depression = max(0.,1-distance/(width*1.3))
                elevation -= 4*depression*depression
            vertices.append([round(x,4),round(elevation,4),round(z,4)])
    triangles=[]
    for j in range(resolution-1):
        for i in range(resolution-1):
            a=j*resolution+i
            triangles.extend([[a,a+1,a+resolution+1],[a,a+resolution+1,a+resolution]])
    env["terrain_mesh"]={"vertices":vertices,"triangles":triangles,"color":"#758265"}
    # A raised crossing is visible in both engines. Geometry is deliberately
    # primitive; a true structural arch requires a later asset pass.
    details=result.setdefault("details",[])
    existing={d["id"] for d in details}
    mid=len(points)//2
    p=points[mid]
    # Bridge is a raised deck across the river, approximately perpendicular
    # to the local flow, not a river-spanning line along the flow.
    before,after=points[max(0,mid-1)],points[min(len(points)-1,mid+1)]
    direction=math.atan2(after[0]-before[0],after[2]-before[2])
    bridge_id="v6_3_bridge_deck"
    if bridge_id in existing:
        raise ValueError("Scene already has V6.3b bridge")
    details.append({"id":bridge_id,"shape":"box",
                    "position":[p[0],2.0,p[2]],
                    "size":[width+8,2.0,8.0],"color":"#a79a83",
                    "rotation_y":direction,"sides":4})
    result["version_note"]="V6.3b terrain mesh, carved river corridor, bridge deck"
    return result

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("input_scene")
    parser.add_argument("output_scene")
    parser.add_argument("--resolution",type=int,default=49)
    args=parser.parse_args()
    result=upgrade(json.loads(Path(args.input_scene).read_text(encoding="utf-8-sig")),args.resolution)
    target=Path(args.output_scene)
    target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding="utf-8")
    print("V6.3b terrain vertices:",len(result["environment"]["terrain_mesh"]["vertices"]))

if __name__=="__main__":
    main()
