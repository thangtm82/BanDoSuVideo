// V4.1: deterministic subject-aware camera composition without Three.js.
// The shot schema remains backward compatible: camera.framing is opt-in.
import {visibleAt} from './history-events.mjs';

const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
const mean=(a,b)=>(a+b)/2;
export function activeSubjectBounds(history,year,{includeWalls=false,focusId=null}={}){
  const items=(history.buildings||[]).filter(b=>
    visibleAt(history,b,year) &&
    (includeWalls || !['wall','gate','tower'].includes(b.kind)) &&
    Array.isArray(b.p)&&b.p.length===2);
  const focused=focusId?(history.buildings||[]).filter(b=>b.id===focusId && visibleAt(history,b,year)):[];
  const list=focused.length?focused:items;
  if(!list.length)return null;
  let minX=Infinity,maxX=-Infinity,minZ=Infinity,maxZ=-Infinity;
  for(const b of list){
    const [x,z]=b.p;
    const pad=(b.kind==='temple'||b.kind==='market'?26:b.kind==='wall'?20:12)*(Number(b.scale)||1);
    minX=Math.min(minX,x-pad);maxX=Math.max(maxX,x+pad);
    minZ=Math.min(minZ,z-pad);maxZ=Math.max(maxZ,z+pad);
  }
  return {center:[mean(minX,maxX),10,mean(minZ,maxZ)],
    radius:Math.max(24,Math.hypot(maxX-minX,maxZ-minZ)/2),
    width:maxX-minX,depth:maxZ-minZ,count:list.length};
}
export function directedCamera(history,year,settings={},t=0){
  const framing=settings.framing;
  if(!['overview','medium','close'].includes(framing))return null;
  const bounds=activeSubjectBounds(history,year,{
    includeWalls:framing==='overview',
    focusId:framing==='close'?settings.focusId:null
  });
  if(!bounds)return null;
  const maxRadius=Number.isFinite(settings.maxRadius)?settings.maxRadius:Infinity;
  const desired=framing==='overview'?Math.max(260,bounds.radius):
    framing==='close'?Math.min(85,bounds.radius):bounds.radius;
  const radius=clamp(Math.min(maxRadius,desired),24,1000);
  const multiplier=framing==='overview'?2.75:framing==='close'?2.85:2.6;
  const distance=clamp(radius*multiplier,
    framing==='close'?145:framing==='medium'?190:450,
    framing==='close'?340:framing==='medium'?800:1300);
  const azimuth=(Number(settings.azimuth) || 43)*Math.PI/180;
  const elevation=(Number(settings.elevation)||34)*Math.PI/180;
  const swing=(Number(settings.sweepDegrees)||0)*Math.PI/180*clamp(t,0,1);
  const angle=azimuth+swing;
  const center=settings.target && settings.target.length===3?
    settings.target.map(Number):bounds.center;
  const horizontal=distance*Math.cos(elevation);
  const position=[center[0]+horizontal*Math.sin(angle),
                  center[1]+distance*Math.sin(elevation),
                  center[2]+horizontal*Math.cos(angle)];
  return {position,target:center,bounds,framing,distance};
}
export function cameraQuality(result){
  if(!result)return [];
  const warnings=[];
  if(result.framing==='medium' && result.bounds.count===0)warnings.push('NO_SUBJECT');
  if(result.framing==='close' && result.bounds.count>1)warnings.push('CLOSEUP_HAS_MULTIPLE_SUBJECTS');
  if(result.framing==='medium' && result.bounds.radius>300)warnings.push('SUBJECT_TOO_WIDE');
  if(result.framing==='overview' && result.bounds.radius>450)warnings.push('LARGE_SCENE');
  return warnings;
}
