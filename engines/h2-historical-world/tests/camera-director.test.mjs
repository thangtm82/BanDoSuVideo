import test from 'node:test';
import assert from 'node:assert/strict';
import {activeSubjectBounds,directedCamera,cameraQuality} from '../engine/camera-director.mjs';
const history={buildings:[
  {id:'house-1',kind:'house',p:[0,0],from:1000},
  {id:'house-2',kind:'house',p:[50,0],from:1100},
  {id:'fort-gate',kind:'gate',p:[-200,0],from:1200},
  {id:'fort-wall',kind:'wall',p:[-200,40],from:1200}
]};
test('camera framing adapts to settlements and different epochs',()=>{
  const early=directedCamera(history,1000,{framing:'medium'},.3);
  const growth=directedCamera(history,1100,{framing:'medium'},.3);
  assert.ok(early.distance<=growth.distance);
  const overview=directedCamera(history,1200,{framing:'overview'},.3);
  assert.ok(overview.bounds.count>growth.bounds.count);
  assert.ok(overview.distance>growth.distance);
});
test('close-up can focus on a historically visible gate',()=>{
  assert.equal(activeSubjectBounds(history,1000,{focusId:'fort-gate'}).count,1);
  const close=directedCamera(history,1200,{framing:'close',focusId:'fort-gate'});
  assert.equal(close.bounds.count,1);
  assert.deepEqual(close.bounds.center.slice(0,1),[-200]);
  assert.ok(close.distance<400);
});
test('forward/back camera scrub is deterministic and legacy camera remains unchanged',()=>{
  const cfg={framing:'medium',azimuth:38,sweepDegrees:12};
  const first=directedCamera(history,1100,cfg,.25);
  directedCamera(history,1100,cfg,.8);
  assert.deepEqual(directedCamera(history,1100,cfg,.25),first);
  assert.equal(directedCamera(history,1100,{mode:'dolly'},.25),null);
  assert.deepEqual(cameraQuality(null),[]);
});
