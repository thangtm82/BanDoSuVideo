#!/usr/bin/env python3
"""Mix voice clips into silent MP4 without relying on codepage-specific stdout."""
import argparse,shutil,subprocess,sys,json
from pathlib import Path
sys.stdout.reconfigure(encoding='utf-8',errors='replace')
sys.stderr.reconfigure(encoding='utf-8',errors='replace')
p=argparse.ArgumentParser();p.add_argument('--project',required=True);a=p.parse_args()
root=Path(a.project).resolve()
video=root/'build/video_silent.mp4'
if not video.exists(): p.error('Render the full video before mixing audio')
destination=root/'out';destination.mkdir(parents=True,exist_ok=True)
output=destination/'video_final.mp4'
story=json.loads((root/'story/shots.json').read_text(encoding='utf-8-sig'))
clips=[]
for shot in story['shots']:
    for ext in ('.mp3','.wav','.m4a'):
        file=root/'input/voice'/(shot['id']+ext)
        if file.exists():
            clips.append((file,round(float(shot['start'])*1000)));break
if not clips:
    shutil.copy2(video,output)
    print('No voice clips. Copied silent video:',output)
    sys.exit(0)
args=['ffmpeg','-hide_banner','-loglevel','error','-y','-i',str(video)]
for file,_ in clips:args.extend(['-i',str(file)])
filters=[]
for i,(_,start) in enumerate(clips,1):
    filters.append(f'[{i}:a]aresample=async=1,adelay={start}:all=1[a{i}]')
filters.append(''.join(f'[a{i}]' for i in range(1,len(clips)+1))+
               f'amix=inputs={len(clips)}:duration=longest:normalize=0,'+
               'alimiter=limit=0.95[aout]')
args.extend(['-filter_complex',';'.join(filters),'-map','0:v:0',
  '-map','[aout]','-c:v','copy','-c:a','aac','-b:a','192k','-shortest',str(output)])
subprocess.run(args,check=True)
if not output.exists() or output.stat().st_size<1024:raise RuntimeError('Final video missing')
print('Video hoan chinh:',output)
