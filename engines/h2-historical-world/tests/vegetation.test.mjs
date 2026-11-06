import test from 'node:test';
import assert from 'node:assert/strict';
import {canopyScale,canopyTint} from '../engine/vegetation.mjs';
const world={river:{pts:[[0,-500],[0,500]],width:12}};
test('city and water corridors remain clear',()=>{
  assert.equal(canopyScale(world,30,30,.1),0);
  assert.equal(canopyScale(world,2,300,.1),0);
  assert.ok(canopyScale(world,310,310,.05)>0);
});
test('forest density and color are deterministic',()=>{
  const samples=[[300,200,.2],[400,-100,.4],[100,380,.7]];
  assert.deepEqual(samples.map(v=>canopyScale(world,...v)),samples.map(v=>canopyScale(world,...v)));
  assert.ok(samples.every(v=>Number.isFinite(canopyTint(...v))));
});
