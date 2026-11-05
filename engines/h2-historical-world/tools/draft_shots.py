#!/usr/bin/env python3
"""Rebuild draft Historical World shots from prepared voice timeline."""
import argparse,json
from pathlib import Path
p=argparse.ArgumentParser()
p.add_argument('--project',required=True)
p.add_argument('--min',type=float,default=3)
p.add_argument('--max',type=float,default=7.5)
a=p.parse_args()
root=Path(a.project).resolve()
history=json.loads((root/'history/world.json').read_text(encoding='utf-8-sig'))
lines=json.loads((root/'timeline/lines.json').read_text(encoding='utf-8-sig'))['lines']
epochs=history['epochs']
if not epochs: p.error('Historical world has no epochs')
old=json.loads((root/'story/shots.json').read_text(encoding='utf-8-sig'))
preserved={s['id']:s for s in old['shots']}
shots=[]
for i,line in enumerate(lines):
    before=preserved.get(line['id'])
    args=before.get('args') if before else None
    if args is None:
        args={'epoch':epochs[min(i,len(epochs)-1)]['id'],
              'camera':{'from':[680,650,700],'to':[540,460,540],'target':[0,20,0]}}
    shots.append(dict(id=line['id'],start=line['start'],end=line['end'],
                      line=line.get('line',''),tpl='epoch3d',args=args))
if not shots or any(s['end']<=s['start'] for s in shots):p.error('Invalid narration timeline')
(root/'story/shots.json').write_text(json.dumps({**old,'shots':shots},
                            ensure_ascii=False,indent=2),encoding='utf-8')
print('Drafted',len(shots),'Historical World scenes')
