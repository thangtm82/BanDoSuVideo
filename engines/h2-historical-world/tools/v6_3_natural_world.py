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
        # Local X runs along the road; local Z spans its width.
        deck_length=width+10.0
        deck_width=8.0
        details.append({"id":bridge_id,"shape":"box",
                        "position":[round(x,4),2.0,round(z,4)],
                        "size":[deck_length,2.0,deck_width],"color":"#b5a38b",
                        "rotation_y":road_angle,"sides":4})
        # Paired parapets follow the bridge orientation in both renderers.
        nx,nz=math.sin(road_angle),math.cos(road_angle)
        for side in (-1,1):
            rail_id=bridge_id+"-parapet-"+str(side)
            details.append({"id":rail_id,"shape":"box",
                            "position":[round(x+side*nx*deck_width*.45,4),
                                        3.7,round(z+side*nz*deck_width*.45,4)],
                            "size":[deck_length,1.8,.75],
                            "color":"#918371","rotation_y":road_angle,"sides":4})
        for end in (-1,1):
            for side in (-1,1):
                along=end*deck_length*.44
                across=side*deck_width*.45
                px=x+math.cos(road_angle)*along+math.sin(road_angle)*across
                pz=z-math.sin(road_angle)*along+math.cos(road_angle)*across
                details.append({"id":bridge_id+"-post-"+str(end)+"-"+str(side),
                    "shape":"box","position":[round(px,4),3.7,round(pz,4)],
                    "size":[1.3,2.5,1.3],"color":"#a99b83",
                    "rotation_y":road_angle,"sides":4})
    env["bridge_crossings"]=[{"x":round(h[0],4),"z":round(h[1],4)}
                             for h in intersections]
    # Mark unresolved river/wall conflicts for a later gate-cutout geometry pass.
    # Cut physical openings in wall spans intersected by the river.
    # Each wall is oriented along its local X axis. Split into two shorter
    # cuboids, leaving a gap wider than the water. No fake dark decal.
    # Existing wall battlements are removed/rebuilt to match new spans.
    walls=[]
    cut_ids=set()
    gap=width+6.0
    for wall in result["instances"]:
        if wall.get("kind") not in ("wall","gate"):
            walls.append(wall)
            continue
        x,y,z=wall["position"]
        length,height,depth=wall["dimensions"]
        angle=float(wall.get("rotation_y",0))
        ux,uz=math.cos(angle),-math.sin(angle)
        a=[x-ux*length/2,0,z-uz*length/2]
        b=[x+ux*length/2,0,z+uz*length/2]
        hits=[crossing(a,b,c,d) for c,d in zip(points,points[1:])]
        hits=[h for h in hits if h is not None]
        if not hits:
            walls.append(wall)
            continue
        hit=min(hits,key=lambda h:math.hypot(h[0]-x,h[1]-z))
        offset=(hit[0]-x)*ux+(hit[1]-z)*uz
        left_end=max(-length/2,min(length/2,offset-gap/2))
        right_start=max(-length/2,min(length/2,offset+gap/2))
        spans=[(-length/2,left_end),(right_start,length/2)]
        cut_ids.add(wall["id"])
        # Original gate facade decals would otherwise float across the gap.
        for side,(lo,hi) in enumerate(spans):
            span=hi-lo
            if span<1.0:
                continue
            mid=(lo+hi)/2
            piece=json.loads(json.dumps(wall))
            piece["id"]=wall["id"]+"-watergate-"+str(side)
            piece["position"]=[round(x+ux*mid,4),y,round(z+uz*mid,4)]
            piece["dimensions"][0]=round(span,4)
            walls.append(piece)
            # New battlements sit on the shortened wall segment.
            count=max(1,int(span//5))
            for k in range(count):
                along=(k-(count-1)/2)*span/count
                details.append({"id":piece["id"]+"-merlon-"+str(k),
                    "shape":"box",
                    "position":[round(piece["position"][0]+ux*along,4),
                                round(y+height/2+1.3,4),
                                round(piece["position"][2]+uz*along,4)],
                    "size":[round(span/count*.55,4),2.6,3.5],
                    "color":"#b2a38d","rotation_y":angle,"sides":8})
    if cut_ids:
        details[:]=[d for d in details if not any(
            (d["id"].startswith(wid+"-merlon-") or d["id"]==wid+"-arch-dark") for wid in cut_ids)]
    result["instances"]=walls
    env["watergate_cut_wall_ids"]=sorted(cut_ids)
    # True means there is a remaining unresolved crossing, not merely
    # that this particular scene happened to have no walls to cut.
    env["river_wall_intersections_unresolved"]=False
    env["watergate_status"]="open_channel_no_arch"
    env["watergate_review_required"]=bool(cut_ids)
    result["version_note"]="V6.3 Final candidate: river crossings, open water channels and parapet bridges"
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
