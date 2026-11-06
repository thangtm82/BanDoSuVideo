#!/usr/bin/env python3
"""Copy voice assets and attach sequential narration to existing scene times."""
import argparse,json,shutil
from pathlib import Path
p=argparse.ArgumentParser()
p.add_argument('--project',required=True)
p.add_argument('--voice-dir',required=True)
p.add_argument('--lines',required=True)
p.add_argument('--lang',default='vi')
a=p.parse_args()
root=Path(a.project).resolve()
if not (root/'config.json').exists(): p.error('Not a project: '+str(root))
source=Path(a.lines)
if not source.exists(): p.error('Missing narration TXT')
lines=[line.strip() for line in source.read_text(encoding='utf-8-sig').splitlines() if line.strip()]
shots=json.loads((root/'story/shots.json').read_text(encoding='utf-8-sig'))['shots']
if len(lines)!=len(shots): p.error('Voice lines count must match the current shots count')
voices=sorted((p for p in Path(a.voice_dir).iterdir() if p.suffix.lower() in ('.mp3','.wav','.m4a')),key=lambda f:f.name)
dest=root/'input/voice';dest.mkdir(parents=True,exist_ok=True)
if voices and len(voices)!=len(lines):p.error('Voice files count must match narration lines count')
for i,src in enumerate(voices):
    target=dest/(shots[i]['id']+src.suffix.lower())
    if src.resolve()!=target.resolve():shutil.copy2(src,target)
for i,shot in enumerate(shots):shot['line']=lines[i]
(root/'story/shots.json').write_text(json.dumps({'shots':shots},ensure_ascii=False,indent=2),encoding='utf-8')
(root/'timeline').mkdir(parents=True,exist_ok=True)
(root/'timeline/lines.json').write_text(json.dumps({'lines':[
 {'id':s['id'],'start':s['start'],'end':s['end'],'line':s['line']} for s in shots
]},ensure_ascii=False,indent=2),encoding='utf-8')
print('Prepared',len(shots),'historical narration scenes')
