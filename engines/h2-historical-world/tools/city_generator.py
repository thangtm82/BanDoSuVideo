#!/usr/bin/env python3
"""Synthetic, seeded historical town: phased streets, varied houses and walls."""
import math
import random

RIVER = [[-600,-550],[-380,-350],[-180,-100],[60,100],[350,370],[590,560]]

def distance_to_polyline(p, pts=RIVER):
    x,z=p
    best=float("inf")
    for (ax,az),(bx,bz) in zip(pts,pts[1:]):
        dx,dz=bx-ax,bz-az
        t=max(0,min(1,((x-ax)*dx+(z-az)*dz)/(dx*dx+dz*dz)))
        best=min(best, math.hypot(x-ax-t*dx,z-az-t*dz))
    return best

def create_city(seed=128):
    rng=random.Random(seed)
    buildings=[]
    roads=[]
    # Narrow, partially irregular axes. Road age follows settlement expansion.
    for axis in ('x','z'):
        for index,c in enumerate((-160,-80,0,80,160)):
            # Shorter roads for outer neighborhoods; no full-map diagonal overlays.
            extent=112 if c==0 else 182 if abs(c)==80 else 213
            jitter=4 if c else 0
            pts=[[c-jitter,-extent],[c+jitter,0],[c-jitter,extent]] if axis=='x' else [
                [-extent,c-jitter],[0,c+jitter],[extent,c-jitter]]
            first_year=1000 if c==0 else 1100 if abs(c)==80 else 1200
            roads.append(dict(id=f'{axis}-{index}',pts=pts,
                kind='primary' if c==0 else 'secondary',**{'from':first_year}))
    # Local alleys only where inhabitants are established.
    for z in (-120,120):
        roads.append(dict(id=f'alley-{z}',pts=[[-110,z-3],[0,z],[110,z+3]],
            kind='alley',**{'from':1100}))
    # Reserve the landmark footprints before placing houses.
    landmarks=[
        ('main-market','market',[-54,15],1100,1.45),
        ('town-hall','temple',[62,-42],1100,1.7),
        ('hill-shrine','temple',[-133,145],1200,1.3),
        ('bridge-guard','tower',[-40,28],1100,1.1)]
    reserved=[(p,24*scale) for _,_,p,_,scale in landmarks]
    palette=('#b99176','#c6ad87','#a98d74','#d0b495','#bfa88e','#a88c72')
    variants=('gable','hip','courtyard','longhouse','workshop','storehouse','two-storey','terraced')
    idx=0
    # Compact blocks with housing fronts set back from streets; no intersections
    # are populated with houses. Block coordinates stable across all epochs.
    for bx in range(-3,3):
        for bz in range(-3,3):
            # Adjacent neighborhoods are slightly offset, reducing rigid grid appearance.
            block_x=bx*80+(7 if bz%2 else -4)
            block_z=bz*80+(5 if bx%2 else -5)
            center_x,center_z=block_x+40,block_z+40
            distance=max(abs(center_x),abs(center_z))
            phase=1000 if distance<105 else 1100 if distance<180 else 1200
            for ox in (19,40,61):
                for oz in (19,40,61):
                    x=block_x+ox+rng.uniform(-1,1)
                    z=block_z+oz+rng.uniform(-1,1)
                    if max(abs(x),abs(z))>225 or distance_to_polyline((x,z))<32:
                        continue
                    if any(math.hypot(x-p[0],z-p[1])<r for p,r in reserved):
                        continue
                    # Small, seeded pockets of gardens/courtyards break repetition.
                    if rng.random()<(0.1 if phase==1000 else 0.19):
                        continue
                    idx+=1
                    buildings.append(dict(id=f'house-{idx:04}',p=[round(x,2),round(z,2)],
                        kind='house',district='center' if phase==1000 else 'outer',
                        variant=variants[(idx+bx*3+bz*5)%len(variants)],
                        color=palette[(idx+bx+bz)%len(palette)],**{'from':phase},
                        rotation=round(rng.choice((0,math.pi/2)),3),
                        scale=round(rng.uniform(.67,1.1),2)))
    for name,kind,p,year,scale in landmarks:
        buildings.append(dict(id=name,kind=kind,p=p,**{'from':year},scale=scale))
    # Continuous perimeter: walls span 32 units with half-width 16.
    # Four gates replace central wall segments, with corner towers.
    w=240
    for side in range(4):
        for step in range(-7,8):
            d=step*32
            x,z=((-w,d) if side==0 else (w,d) if side==1 else (d,-w) if side==2 else (d,w))
            kind='gate' if step==0 else 'tower' if abs(step)==7 else 'wall'
            buildings.append(dict(id=f'fort-{side}-{step+7:02}',p=[x,z],kind=kind,
                **{'from':1200},rotation=math.pi/2 if side<2 else 0,scale=1))
    events=[
        dict(id='market-opens',year=1100,changes=[dict(id='main-market',action='show')]),
        dict(id='fortification-completed',year=1200,
            changes=[dict(id=b['id'],action='show') for b in buildings if b['id'].startswith('fort-')])
    ]
    return buildings,roads,events
