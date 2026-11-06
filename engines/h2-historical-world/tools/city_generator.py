#!/usr/bin/env python3
"""Deterministic synthetic planned town with 3 historical development phases."""
import math
import random

def create_city(seed=128):
    rng = random.Random(seed)
    buildings = []
    roads = []
    # Primary axes and four side streets; streets are reusable historical geometry.
    for x in (-160,-80,0,80,160):
        roads.append([[x,-230],[x,230]])
    for z in (-160,-80,0,80,160):
        roads.append([[-230,z],[230,z]])
    roads.extend([[[-250,-240],[250,240]],[[-250,240],[250,-240]]])
    # River runs near center; reserve a corridor for bridges and riverbanks.
    river = [[-600,-550],[-380,-350],[-180,-100],[60,100],[350,370],[590,560]]
    def river_distance(x,z):
        best=1e9
        for (ax,az),(bx,bz) in zip(river,river[1:]):
            dx,dz=bx-ax,bz-az
            t=max(0,min(1,((x-ax)*dx+(z-az)*dz)/(dx*dx+dz*dz)))
            best=min(best,math.hypot(x-ax-dx*t,z-az-dz*t))
        return best
    # Houses align with the street grid and emerge by district phase.
    idx=0
    for col in range(-9,10):
        for row in range(-9,10):
            x,z=col*24+12,row*24+12
            if abs(x)>228 or abs(z)>228 or river_distance(x,z)<28:
                continue
            if abs(x)<33 and abs(z)<33:
                continue
            dist=max(abs(x),abs(z))
            phase=1000 if dist<105 else 1100 if dist<180 else 1200
            if rng.random()<(.93 if phase==1000 else .78):
                idx+=1
                district='market' if abs(x)<75 and abs(z)<100 else 'residential'
                buildings.append(dict(id=f'house-{idx:04}',p=[x+rng.uniform(-2,2),z+rng.uniform(-2,2)],
                    kind='house',district=district,**{'from':phase},
                    rotation=round(rng.choice([0,math.pi/2]),3),
                    scale=round(rng.uniform(.75,1.18),2)))
    for name,kind,p,year,scale in [
        ('main-market','market',[-55,10],1100,1.9),
        ('town-hall','temple',[60,-35],1100,2.0),
        ('hill-shrine','temple',[-115,170],1200,1.5),
        ('bridge-guard','tower',[-40,28],1100,1.2)]:
        buildings.append(dict(id=name,kind=kind,p=p,**{'from':year},scale=scale))
    # The enclosed town is visually distinct only in the final epoch.
    # Gates replace some wall segments on cardinal axes.
    w=246
    for side in range(4):
        for step in range(-7,8):
            d=step*32
            x,z=((-w,d) if side==0 else (w,d) if side==1 else (d,-w) if side==2 else (d,w))
            kind='gate' if step==0 else 'tower' if abs(step)==7 else 'wall'
            buildings.append(dict(id=f'fort-{side}-{step+7:02}',p=[x,z],kind=kind,
                **{'from':1200},rotation=math.pi/2 if side<2 else 0,scale=1))
    # Explicit event log can be extended by authors, preserving stable object IDs.
    events=[
        dict(id='town-market-opens',year=1100,changes=[dict(id='main-market',action='show')]),
        dict(id='fortifications-completed',year=1200,
             changes=[dict(id=b['id'],action='show') for b in buildings if b['id'].startswith('fort-')])
    ]
    return buildings,roads,events
