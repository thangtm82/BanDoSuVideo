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
    if scene.get("instances"):
        landmarks=[o for o in scene["instances"] if o.get("kind") in ("temple","market")]
        if landmarks:
            landmark=max(landmarks,key=lambda o:o.get("dimensions",[0,0,0])[1])
            target=list(landmark["position"])
            target[1]+=landmark["dimensions"][1]*.25
    # Three continuous phases: establishing orbit, dolly-in, close fly-by.
    # Smoothstep avoids abrupt speed changes at phase boundaries.
    dx,dz=start[0]-target[0],start[2]-target[2]
    radius=math.hypot(dx,dz)
    initial=math.atan2(dz,dx)
    def smooth(t):
        t=max(0.,min(1.,t))
        return t*t*(3-2*t)
    shots=[]
    for frame in range(frames):
        t=frame/(frames-1)
        if t < .38:
            phase="establishing"
            u=smooth(t/.38)
            distance=radius*(1-.13*u)
            angle=initial+math.radians(-16+20*u)
            elevation=start[1]+12*u
        elif t < .76:
            phase="approach"
            u=smooth((t-.38)/.38)
            distance=radius*(.87-.37*u)
            angle=initial+math.radians(4+13*u)
            elevation=start[1]+12-75*u
        else:
            phase="flyby"
            u=smooth((t-.76)/.24)
            distance=radius*(.50-.08*u)
            angle=initial+math.radians(17+20*u)
            elevation=max(target[1]+35,start[1]-63-12*u)
        shots.append({"frame":frame+1,"phase":phase,
                      "position":[round(target[0]+distance*math.cos(angle),4),
                                  round(elevation,4),
                                  round(target[2]+distance*math.sin(angle),4)],
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
    mux.add_argument("--ffmpeg",default="ffmpeg")
    args=p.parse_args()
    if args.action=="plan":
        result=camera_path(json.loads(Path(args.scene).read_text(encoding="utf-8-sig")),args.frames,args.fps)
        dest=Path(args.output);dest.parent.mkdir(parents=True,exist_ok=True)
        dest.write_text(json.dumps(result,indent=2),encoding="utf-8")
        print("Camera plan:",dest,"frames:",args.frames)
    else:
        print("MP4:",assemble(args.frames_dir,args.output,args.fps,args.ffmpeg))
if __name__=="__main__":
    main()
