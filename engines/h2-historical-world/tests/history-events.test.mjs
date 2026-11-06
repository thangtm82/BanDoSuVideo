import test from 'node:test';
import assert from 'node:assert/strict';
import {validateEvents,visibleAt,epochSummary} from '../engine/history-events.mjs';

const history={
  buildings:[
    {id:'house-a',kind:'house',p:[0,0],from:1000},
    {id:'wall-a',kind:'wall',p:[20,0],from:1200},
    {id:'shrine',kind:'temple',p:[30,0],from:1000}
  ],
  events:[
    {id:'rebuild',year:1200,changes:[{id:'shrine',action:'show'}]},
    {id:'damage',year:1100,changes:[{id:'shrine',action:'hide'}]}
  ]
};
test('events can be applied in either scrub direction without mutation',()=>{
  validateEvents(history);
  assert.equal(visibleAt(history,history.buildings[2],1000),true);
  assert.equal(visibleAt(history,history.buildings[2],1150),false);
  assert.equal(visibleAt(history,history.buildings[2],1250),true);
  assert.equal(visibleAt(history,history.buildings[2],1150),false);
  assert.equal(epochSummary(history,1000).wall,0);
  assert.equal(epochSummary(history,1200).wall,1);
});
test('invalid IDs, duplicate events or unknown actions fail',()=>{
  assert.throws(()=>validateEvents({...history,events:[
    {id:'bad',year:1100,changes:[{id:'missing',action:'show'}]}
  ]}),/Invalid event change/);
  assert.throws(()=>validateEvents({...history,events:[
    {id:'same',year:1100,changes:[]},{id:'same',year:1200,changes:[]}
  ]}),/Invalid historical event/);
  assert.throws(()=>validateEvents({...history,events:[
    {id:'bad',year:1100,changes:[{id:'house-a',action:'destroy'}]}
  ]}),/Invalid event change/);
});
test('V2 projects without events remain compatible',()=>{
  const old={buildings:[{id:'a',kind:'house',from:1000,to:1100}]};
  validateEvents(old);
  assert.equal(visibleAt(old,old.buildings[0],1000),true);
  assert.equal(visibleAt(old,old.buildings[0],1100),false);
});
