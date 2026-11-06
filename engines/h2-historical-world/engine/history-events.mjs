// V3 historical change log: stable object IDs, deterministic forward/backward evaluation.
// A history event may reveal or retire IDs from its year onward; no scene mutation.
export function validateEvents(history) {
  const buildings=new Set((history.buildings||[]).map(b=>b.id));
  const ids=new Set();
  for(const event of history.events||[]){
    if(!event || typeof event.id!=='string' || !event.id || ids.has(event.id) ||
       !Number.isFinite(event.year) || !Array.isArray(event.changes))
      throw new Error('Invalid historical event');
    ids.add(event.id);
    for(const change of event.changes){
      if(!buildings.has(change.id) || !['show','hide'].includes(change.action))
        throw new Error('Invalid event change: '+change.id);
    }
  }
}
export function visibleAt(history,building,year){
  let visible=building.from<=year && (building.to==null || year<building.to);
  // Events are applied in chronological order, independent of array order and scrub direction.
  const events=(history.events||[]).filter(e=>e.year<=year)
    .sort((a,b)=>a.year-b.year||a.id.localeCompare(b.id));
  for(const event of events){
    for(const change of event.changes){
      if(change.id===building.id) visible=change.action==='show';
    }
  }
  return visible;
}
export function epochSummary(history,year){
  const counts={house:0,market:0,temple:0,tower:0,wall:0,gate:0};
  for(const b of history.buildings||[]){
    if(visibleAt(history,b,year)){
      const kind=b.kind||'house';
      counts[kind]=(counts[kind]||0)+1;
    }
  }
  return counts;
}
