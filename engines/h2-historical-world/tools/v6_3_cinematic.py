"""V6.3c deterministic cinematic camera planning and FFmpeg frame assembly.

Generate a camera-path manifest; Blender frame rendering is handled by
v6_3_blender_animation.py. FFmpeg is invoked only on explicit assemble.
"""
import argparse
import json
import math
import shutil
import subprocess
from pathlib import Path

def camera_path(scene, frames=120, fps=24):
    if frames < 2 or fps <= 0:
        raise ValueError("frames >= 2 and fps > 0 required")
    cameras=scene.get("cameras") or []
    base=cameras[0] if cameras else {"position":[520,260,500],"target":[0,0,0],"fov":45}
    start=base["position"]
    target=base["target"]
    # Gentle orbit around the target; maintain radius and elevation.
    dx,dz=start[0]-target[0],start[2]-target[2]
    radius=math.hypot(dx,dz)
    initial=math.atan2(dz,dx)
    shots=[]
    for frame in range(frames):
        t=frame/(frames-1)
        angle=initial+(t-.5)*math.radians(24)
        shots.append({"frame":frame+1,"position":[round(target[0]+radius*math.cos(angle),4),
                       round(start[1]+12*math.sin(math.pi*t),4),
                       round(target[2]+radius*math.sin(angle),4)],
                      "target":target,"fov":base.get("fov",45)})
    return {"schema":"h2.historical_world.cinematic.v6.3",
            "fps":fps,"frames":frames,"shots":shots}

def assemble(frames_dir, output, fps=24, ffmpeg="ffmpeg"):
    folder=Path(frames_dir)
    frames=sorted(folder.glob("frame_*.png"))
    if not frames:
        raise FileNotFoundError("No frame_*.png files in "+str(folder))
    if [f.name for f in frames] != ["frame_%05d.png"%i for i in range(1,len(frames)+1)]:
        raise ValueError("Missing or noncontiguous numbered frames")
    if not shutil.which(ffmpeg) and not Path(ffmpeg).is_file():
        raise FileNotFoundError("FFmpeg executable not found")
    target=Path(output)
    target.parent.mkdir(parents=True,exist_ok=True)
    cmd=[ffmpeg,"-y","-framerate",str(fps),"-start_number","1",
         "-i",str(folder/"frame_%05d.png"),"-c:v","libx264",
         "-pix_fmt","yuv420p","-crf","19",str(target)]
    subprocess.run(cmd,check=True)
    if not target.is_file() or target.stat().st_size==0:
        raise RuntimeError("FFmpeg produced no output")
    return target

def main():
    p=argparse.ArgumentParser()
    sub=p.add_subparsers(dest="action",required=True)
    plan=sub.add_parser("plan")
    plan.add_argument("scene")
    plan.add_argument("output")
    plan.add_argument("--frames",type=int,default=120)
    plan.add_argument("--fps",type=int,default=24)
    mux=sub.add_parser("assemble")
    mux.add_argument("frames_dir")
    mux.add_argument("output")
    mux.add_argument("--fps",type=int,default=24)
    args=p.parse_args()
    if args.action=="plan":
        result=camera_path(json.loads(Path(args.scene).read_text(encoding="utf-8-sig")),args.frames,args.fps)
        dest=Path(args.output);dest.parent.mkdir(parents=True,exist_ok=True)
        dest.write_text(json.dumps(result,indent=2),encoding="utf-8")
        print("Camera plan:",dest,"frames:",args.frames)
    else:
        print("MP4:",assemble(args.frames_dir,args.output,args.fps))
if __name__=="__main__":
    main()
