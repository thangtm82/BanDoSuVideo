#!/usr/bin/env python3
from pathlib import Path
import sys,json
root=Path(__file__).resolve().parent.parent
necessary=['render.mjs','player.html','engine/world.mjs','packs/historical/pack.js','package.json']
missing=[f for f in necessary if not (root/f).exists()]
print('H2 Historical World 3D')
print('Engine:',root)
for f in necessary: print(('OK' if f not in missing else 'MISSING'),f)
for module in ('node_modules/three/build/three.module.js',
               'node_modules/playwright/package.json'):
    print(('OK' if (root/module).exists() else 'MISSING'),module)
sys.exit(bool(missing))
