// Pure deterministic historical timeline helpers, shared by renderer and tests.
export function validateHistory(history) {
  if (!history || !Array.isArray(history.epochs) || history.epochs.length === 0)
    throw new Error('Expected one or more historical epochs');
  let prev = -Infinity;
  const ids = new Set();
  for (const epoch of history.epochs) {
    if (typeof epoch.id !== 'string' || !epoch.id || !Number.isFinite(epoch.year) ||
        epoch.year <= prev || ids.has(epoch.id)) throw new Error('Epochs must have unique IDs and increasing years');
    ids.add(epoch.id); prev = epoch.year;
  }
  const buildings = history.buildings ?? [];
  if (!Array.isArray(buildings)) throw new Error('Invalid buildings');
  const names = new Set();
  for (const b of buildings) {
    if (!b || typeof b.id !== 'string' || !b.id || names.has(b.id) ||
        !Array.isArray(b.p) || b.p.length !== 2 || !b.p.every(Number.isFinite) ||
        !Number.isFinite(b.from) || (b.to != null && !(b.to > b.from)))
      throw new Error('Invalid building lifetime/position: ' + (b?.id ?? '?'));
    names.add(b.id);
  }
  return history;
}
export function epochById(history, id) {
  validateHistory(history);
  const epoch = history.epochs.find(e=>e.id===id);
  if (!epoch) throw new Error('Unknown epoch: ' + id);
  return epoch;
}
export function activeBuildings(history, id) {
  const year = epochById(history, id).year;
  return (history.buildings ?? []).filter(b=>b.from<=year && (b.to==null || year<b.to));
}
export function shotAtTime(story, seconds) {
  if (!story?.shots?.length || !Number.isFinite(seconds)) throw new Error('Invalid timeline');
  return story.shots.find(s=>seconds>=s.start && seconds<s.end) ??
    (seconds>=story.shots.at(-1).end ? story.shots.at(-1) : story.shots[0]);
}
export function shotT(shot, seconds) {
  return Math.max(0,Math.min(1,(seconds-shot.start)/Math.max(0.001,shot.end-shot.start)));
}
export function layout(seed, count, radius) {
  let n=(seed|0)||1;
  const rand=()=>{n^=n<<13;n^=n>>>17;n^=n<<5;return (n>>>0)/4294967296;};
  const points=[];
  for(let i=0;i<count;i++){
    const a=rand()*Math.PI*2,r=Math.sqrt(rand())*radius;
    points.push([Math.cos(a)*r,Math.sin(a)*r,rand()]);
  }
  return points;
}
