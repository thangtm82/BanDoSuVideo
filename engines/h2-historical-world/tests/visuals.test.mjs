import test from 'node:test';
import assert from 'node:assert/strict';
import {terrainHeight,surfaceHeight,nearestRiverDistance,cameraPose,streetSegments,buildingActive} from '../engine/visuals.mjs';

const world={hills:[{p:[80,80],r:70,h:30}],river:{pts:[[-100,0],[100,0]],width:10},
  roads:{paths:[[[0,0],[20,0]]] }};
test('terrain is stable and river carves a channel',()=>{
  assert.equal(terrainHeight(world,80,80),terrainHeight(world,80,80));
  assert.ok(nearestRiverDistance(world,0,0)<.001);
  assert.ok(nearestRiverDistance(world,0,50)>40);
  assert.ok(surfaceHeight(world,0,0)<terrainHeight(world,0,0));
  assert.ok(Number.isFinite(surfaceHeight(world,80,80)));
});
test('camera forward/back scrubbing returns same pose',()=>{
  const settings={from:[200,100,200],to:[100,80,100],target:[0,0,0],mode:'orbit',angle:35};
  const before=cameraPose(settings,.25);
  cameraPose(settings,.9);
  assert.deepEqual(cameraPose(settings,.25),before);
  assert.deepEqual(cameraPose(settings,0).position,settings.from);
  assert.ok(cameraPose(settings,1).position.every(Number.isFinite));
});
test('roads and epoch lifetime remain backwards compatible',()=>{
  assert.equal(streetSegments({}).length,0);
  assert.equal(streetSegments(world).length,1);
  assert.equal(buildingActive({from:1000,to:1100},1000),true);
  assert.equal(buildingActive({from:1000,to:1100},1100),false);
});
