"""Deterministic illustrative town layout, not historically sourced coordinates."""
import math
import random

def create_city(seed=128):
    rng=random.Random(seed)
    buildings=[]
    roads=[]
    # Orthogonal districts provide inspectable streets, rather than random radial piles.
    axes=[-192,-128,-64,0,64,128,192]
    for axis in axes:
        roads.append([[-225,axis],[225,axis]])
        roads.append([[axis,-225],[axis,225]])

    for ix in range(6):
        for iz in range(6):
            cx=-160+ix*64
            cz=-160+iz*64
            # Synthetic river runs roughly diagonally; reserve its banks as open land.
            if abs(cz-cx)<35:
                continue
            distance=math.hypot(cx,cz)
            zone=('market' if abs(cx)<70 and abs(cz)<90
                  else 'temple' if cx<-85 and cz>65
                  else 'residential')
            for px,pz in ((-14,-14),(14,-14),(-14,14),(14,14)):
                x=cx+px+rng.uniform(-3,3)
                z=cz+pz+rng.uniform(-3,3)
                year=1000 if distance<115 else 1100 if distance<195 else 1200
                kind='house'
                if zone=='market' and px<0 and pz<0:kind='market'
                if zone=='temple' and px<0 and pz<0:kind='temple'
                buildings.append(dict(id=f'block-{ix}-{iz}-{len(buildings):03d}',
                  p=[round(x,2),round(z,2)],kind=kind,
                  **{'from':year},rotation=round(rng.choice([0,math.pi/2]),3),
                  scale=round(.85+rng.random()*.3,2),zone=zone))
    # The last epoch has a continuous square perimeter with four deliberate gate openings.
    # Segment length 32, and corner walls meet at the same ground plan coordinates.
    for side in range(4):
        for i in range(-7,8):
            if i==0:continue
            t=i*32
            x,z,angle=(
                (t,-256,0) if side==0 else
                (256,t,math.pi/2) if side==1 else
                (t,256,0) if side==2 else
                (-256,t,math.pi/2))
            buildings.append(dict(id=f'wall-{side}-{i+7:02d}',
                kind='wall',p=[x,z],**{'from':1200},rotation=angle,
                scale=1,zone='fortification'))
        x,z,angle=((0,-256,0) if side==0 else (256,0,math.pi/2)
                   if side==1 else (0,256,0) if side==2 else (-256,0,math.pi/2))
        buildings.append(dict(id=f'gate-{side}',kind='gate',p=[x,z],
                     **{'from':1200},rotation=angle,scale=1,zone='fortification'))
    # Landmark changes are explicitly authored events, not inferred from number of buildings.
    buildings.append(dict(id='old-hall',kind='temple',p=[-75,85],
                          **{'from':1000},zone='civic',scale=1.45))
    buildings.append(dict(id='new-hall',kind='temple',p=[-75,85],
                          **{'from':1200},zone='civic',scale=1.55))
    events=[
        dict(id='urban-expansion',year=1100,title='Expansion of the town',changes=[]),
        dict(id='hall-reconstruction',year=1200,title='Rebuild the civic hall',
             changes=[dict(id='old-hall',action='hide')]),
        dict(id='fortification',year=1200,title='Construction of the city walls',
             changes=[])
    ]
    return buildings,roads,events
