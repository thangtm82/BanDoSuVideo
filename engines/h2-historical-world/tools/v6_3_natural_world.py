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
            triangles.extend([[a,a+resolution+1,a+1],[a,a+resolution,a+resolution+1]])
    env["terrain_mesh"]={"vertices":vertices,"triangles":triangles,"color":"#758265"}
    # Replace legacy background cones: the continuous terrain mesh now owns hills.
    details=result.setdefault("details",[])
    details[:]=[d for d in details if not d.get("id","").startswith("hill-")]
    existing={d["id"] for d in details}
    # Find actual intersections of road and river segments in the horizontal plane.
    # Do not place a decorative bridge at an arbitrary midpoint.
    def crossing(a,b,c,d):
        ax,az=a[0],a[2];bx,bz=b[0],b[2]
        cx,cz=c[0],c[2];dx,dz=d[0],d[2]
        rx,rz=bx-ax,bz-az;sx,sz=dx-cx,dz-cz
        den=rx*sz-rz*sx
        if abs(den)<1e-8:
            return None
        qx,qz=cx-ax,cz-az
        t=(qx*sz-qz*sx)/den
        u=(qx*rz-qz*rx)/den
        if 0<=t<=1 and 0<=u<=1:
            return (ax+t*rx,az+t*rz,rx,rz,sx,sz)
        return None
    intersections=[]
    for road in env.get("roads",[]):
        road_points=road.get("points",[])
        for a,b in zip(road_points,road_points[1:]):
            for c,d in zip(points,points[1:]):
                hit=crossing(a,b,c,d)
                if hit and all(math.hypot(hit[0]-other[0],hit[1]-other[1])>width
                               for other in intersections):
                    intersections.append(hit)
    for i,(x,z,rx,rz,sx,sz) in enumerate(intersections):
        road_angle=math.atan2(-rz,rx)
        bridge_id="v6_3_bridge_"+str(i)
        if bridge_id in existing:
            raise ValueError("Duplicate bridge ID: "+bridge_id)
        existing.add(bridge_id)
        details.append({"id":bridge_id,"shape":"box",
                        "position":[round(x,4),2.0,round(z,4)],
                        "size":[width+10,2.0,8.0],"color":"#b5a38b",
                        "rotation_y":road_angle,"sides":4})
    env["bridge_crossings"]=[{"x":round(h[0],4),"z":round(h[1],4)}
                             for h in intersections]
    # Mark unresolved river/wall conflicts for a later gate-cutout geometry pass.
    env["river_wall_intersections_unresolved"]=True
    result["version_note"]="V6.3b terrain mesh and road-aligned bridge crossings"
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
