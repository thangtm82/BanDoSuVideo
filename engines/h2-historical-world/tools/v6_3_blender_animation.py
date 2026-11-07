"""V6.3c Blender frame rendering from a shared cinematic plan.

Usage: blender -b -t 4 --python tools/v6_3_blender_animation.py --
       scene.json camera_plan.json frames_dir [bright_final]
"""
import json
import math
import sys
from pathlib import Path

def main(scene_file, plan_file, frames_dir, preset="bright_final"):
    import bpy
    from mathutils import Vector
    # Blender does not automatically add the --python script directory to sys.path.
    tools_dir = str(Path(__file__).resolve().parent)
    if tools_dir not in sys.path:
        sys.path.insert(0, tools_dir)
    from v6_2_blender_preview import render
    folder=Path(frames_dir).resolve()
    folder.mkdir(parents=True,exist_ok=True)
    # Build scene using the same geometry and lighting as the still renderer.
    render(scene_file,str(folder/"setup.png"),preset)
    (folder/"setup.png").unlink(missing_ok=True)
    plan=json.loads(Path(plan_file).read_text(encoding="utf-8-sig"))
    if plan.get("schema")!="h2.historical_world.cinematic.v6.3":
        raise ValueError("Invalid camera plan")
    shots=plan.get("shots",[])
    if len(shots)!=plan.get("frames") or not shots:
        raise ValueError("Camera plan frame count mismatch")
    scene=bpy.context.scene
    scene.render.image_settings.file_format="PNG"
    scene.render.film_transparent=False
    camera=scene.camera
    for index,shot in enumerate(shots,1):
        if shot.get("frame")!=index:
            raise ValueError("Camera frames must be sequential")
        x,y,z=shot["position"]
        tx,ty,tz=shot["target"]
        camera.location=(x,z,y)
        target=Vector((tx,tz,ty))
        camera.rotation_euler=(target-camera.location).to_track_quat("-Z","Y").to_euler()
        fov=float(shot.get("fov",45))
        if not 1<fov<179:
            raise ValueError("Invalid camera FOV")
        camera.data.sensor_fit="VERTICAL"
        camera.data.sensor_height=24
        camera.data.lens=24/(2*math.tan(math.radians(fov)/2))
        scene.frame_set(index)
        dest=folder/("frame_%05d.png"%index)
        scene.render.filepath=str(dest)
        bpy.ops.render.render(write_still=True)
        if not dest.is_file() or dest.stat().st_size==0:
            raise RuntimeError("Missing rendered frame: "+str(dest))
        print("H2 V6.3 frame",index,"/",len(shots),flush=True)

if __name__=="__main__":
    args=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    if len(args) not in (3,4):
        raise SystemExit("Usage: blender -b --python v6_3_blender_animation.py -- scene.json plan.json frames_dir [preset]")
    main(*args)
