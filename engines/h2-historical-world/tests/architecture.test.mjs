import test from 'node:test';
import assert from 'node:assert/strict';
import {HOUSE_VARIANTS,variantFor,buildingParts} from '../engine/architecture.mjs';

test('eight distinct residential variants with deterministic fallback',()=>{
  assert.equal(HOUSE_VARIANTS.length,8);
  assert.equal(new Set(HOUSE_VARIANTS).size,8);
  const b={id:'house-0123',kind:'house'};
  assert.equal(variantFor(b),variantFor({...b}));
  assert.equal(variantFor({kind:'house',variant:'workshop'}),'workshop');
  assert.equal(variantFor({id:'gate-1',kind:'gate'}),'gate');
});
// Lightweight geometry facade catches undefined catalog primitives without WebGL.
class Geo{rotateY(){return this;}}
const T={BoxGeometry:Geo,ConeGeometry:Geo,CylinderGeometry:Geo};
test('every architectural type returns valid independent render parts',()=>{
  for(const variant of HOUSE_VARIANTS){
    const parts=buildingParts(T,'house',variant,'#b99176');
    assert.ok(parts.length>=2,variant);
    assert.ok(parts.every(x=>x.geometry && x.color && x.offset.length===3));
  }
  for(const kind of ['wall','gate','tower','temple','market']){
    const parts=buildingParts(T,kind,kind,'#c7a17e');
    assert.ok(parts.length>=2,kind);
  }
  assert.ok(buildingParts(T,'gate','gate').length>2);
});
