#!/usr/bin/env python3
"""Create a synthetic, editable Historical World sample project for BanDoSuVideo."""
import argparse, json, math, random
from pathlib import Path

p=argparse.ArgumentParser()
p.add_argument('--name',required=True)
p.add_argument('--title',required=True)
p.add_argument('--pack',default='historical')
p.add_argument('--lang',default='vi')
p.add_argument('--style',default='flat')
p.add_argument('--captions',action='store_true')
a=p.parse_args()
if not a.name.replace('-','').replace('_','').isalnum() or not a.name[0].isalnum():
    p.error('Invalid project slug')
if a.pack!='historical': p.error('Historical engine only supports --pack historical')
root=Path(__file__).resolve().parent.parent
dest=root/'projects'/a.name
if dest.exists(): p.error('Project exists: '+str(dest))
for directory in ('story','history','timeline','input/voice','build','out'):
    (dest/directory).mkdir(parents=True,exist_ok=True)
def write(relative,value):
    (dest/relative).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
write('config.json',dict(title=a.title,pack='historical',lang=a.lang,style=a.style,
                         fps=24,width=1280,height=720,captions=a.captions))
epochs=[dict(id='early',year=1000,title='Early settlement'),
        dict(id='growth',year=1100,title='Growing town'),
        dict(id='late',year=1200,title='Walled town')]
rng=random.Random(128)
buildings=[]
for i in range(420):
    angle=rng.random()*math.tau
    radius=math.sqrt(rng.random())*220
    x=round(math.cos(angle)*radius,2)
    z=round(math.sin(angle)*radius,2)
    since=epochs[i%3]['year']
    buildings.append(dict(id=f'building-{i+1:04d}',p=[x,z],from_=since))
    buildings[-1]['from']=buildings[-1].pop('from_')
    buildings[-1]['kind']='tower' if i%63==0 else 'house'
    buildings[-1]['rotation']=round(rng.random()*math.tau,3)
    buildings[-1]['scale']=round(.7+rng.random()*.7,2)
write('history/world.json',{
  'world':{'size':1400,'ground':'#a1a287','sky':'#bed6d7',
    'hills':[{'p':[-280,240],'r':270,'h':120},{'p':[380,-310],'r':260,'h':75}],
    'river':{'pts':[[-600,-550],[-380,-350],[-180,-100],[60,100],[350,370],[590,560]],'width':12},
    'vegetation':{'seed':1234,'count':850}},
  'epochs':epochs,'buildings':buildings,
  'evidence':{'status':'illustrative','note':'Fictional city. No historical accuracy claimed.'}
})
shots=[]
for i,e in enumerate(epochs):
    start=i*8
    shots.append(dict(id=f's{i+1:03d}',start=start,end=start+8,
      line=f'Giai đoạn mô phỏng {e["title"]}',tpl='epoch3d',
      args={'epoch':e['id'],'camera':{'from':[680,650,700],
        'to':[520-i*80,450-i*35,520-i*60],'target':[0,20,0]}}))
write('story/shots.json',{'shots':shots})
write('timeline/lines.json',{'lines':[{'id':s['id'],'start':s['start'],
                                     'end':s['end'],'line':s['line']} for s in shots]})
print('Created historical world project:',dest)
