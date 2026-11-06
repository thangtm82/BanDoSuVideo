import test from 'node:test';
import assert from 'node:assert/strict';
import {normalizeRoads,roadPieces,roadVisible,distanceToRiver} from '../engine/roads.mjs';

const world={river:{pts:[[0,-100],[0,100]],width:10},
  roads:{paths:[
    {id:'early',pts:[[-90,0],[90,0]],kind:'primary',from:1000},
    {id:'late',pts:[[-90,40],[-40,40]],kind:'alley',from:1200}
  ]}};
test('roads reveal at historical years without altering geometry',()=>{
  const roads=normalizeRoads(world);
  assert.deepEqual(roads.map(r=>roadVisible(r,1000)),[true,false]);
  assert.deepEqual(roads.map(r=>roadVisible(r,1200)),[true,true]);
  assert.deepEqual(roads.map(r=>roadVisible(r,1000)),[true,false]);
});
test('river clips each bank into separate road pieces',()=>{
  const pieces=roadPieces(world,normalizeRoads(world)[0],3);
  assert.equal(pieces.length,2);
  for(const piece of pieces) for(const [x,z] of piece)
    assert.ok(distanceToRiver(world,x,z)>13);
});
test('legacy V2 arrays remain visible in any epoch',()=>{
  const roads=normalizeRoads({roads:{paths:[[[0,20],[80,20]]]}});
  assert.equal(roads.length,1);
  assert.equal(roadVisible(roads[0],1000),true);
  assert.equal(roadPieces({},roads[0]).length,1);
});
