import test from 'node:test';
import assert from 'node:assert/strict';
import {validateHistory,activeBuildings,shotAtTime,shotT,layout} from '../engine/timeline.mjs';
const h={epochs:[{id:'early',year:1000},{id:'late',year:1200}],buildings:[
{id:'a',p:[0,0],from:900,to:1100},{id:'b',p:[20,20],from:1200}]};
test('epochs give deterministic buildings',()=>{
 assert.deepEqual(activeBuildings(h,'early').map(x=>x.id),['a']);
 assert.deepEqual(activeBuildings(h,'late').map(x=>x.id),['b']);
 assert.throws(()=>activeBuildings(h,'invalid'),/Unknown epoch/);
});
test('validations reject duplicated or backward epochs',()=>{
 assert.throws(()=>validateHistory({...h,epochs:[{id:'a',year:1000},{id:'b',year:900}]}),/increasing years/);
 assert.throws(()=>validateHistory({...h,buildings:[{id:'a',p:[2],from:0}]}),/Invalid building/);
});
test('timeline can scrub backward with deterministic placement',()=>{
 const story={shots:[{id:'s1',start:0,end:4},{id:'s2',start:4,end:10}]};
 assert.equal(shotAtTime(story,7).id,'s2');
 assert.equal(shotAtTime(story,1).id,'s1');
 assert.equal(shotT(story.shots[1],7),0.5);
 assert.deepEqual(layout(11,150,320),layout(11,150,320));
});
