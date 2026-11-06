// Deterministic geometry and cinematography helpers; no browser or Three.js dependency.
export const clamp=(v,lo,hi)=>Math.max(lo,Math.min(hi,v));
export function terrainHeight(world,x,z){
  let h=1.5*Math.sin(x*.012)*Math.cos(z*.017);
  for(const hill of world.hills||[]){
    const r=Math.max(5,Number(hill.r)||100),p=hill.p||[0,0];
    h+=Math.exp(-2*((x-p[0])**2+(z-p[1])**2)/(r*r))*(Number(hill.h)||45);
  }
  // Fine relief is restrained near the settlement, more pronounced in distant hills.
  const cityFade=clamp(Math.hypot(x,z)/260,0,1);
  h+=cityFade*(2.8*Math.sin(x*.047+z*.016)*Math.cos(z*.038));
  return h;
}
export function nearestRiverDistance(world,x,z){
  const pts=world.river?.pts;
  if(!Array.isArray(pts)||pts.length<2)return Infinity;
  let best=Infinity;
  for(let i=1;i<pts.length;i++){
    const [ax,az]=pts[i-1],[bx,bz]=pts[i];
    const dx=bx-ax,dz=bz-az,den=dx*dx+dz*dz;
    const t=den?clamp(((x-ax)*dx+(z-az)*dz)/den,0,1):0;
    best=Math.min(best,Math.hypot(x-ax-t*dx,z-az-t*dz));
  }
  return best;
}
export function surfaceHeight(world,x,z){
  const d=nearestRiverDistance(world,x,z);
  const width=Math.max(2,Number(world.river?.width)||12);
  const bank=clamp((d-width*.7)/(width*2.5),0,1);
  return terrainHeight(world,x,z)-(1-bank)*Math.max(3,width*.34);
}
export function cameraPose(settings,t){
  const from=settings?.from||[620,650,760],to=settings?.to||from;
  const target=settings?.target||[0,0,0];
  const easing=settings?.easing==='linear'?clamp(t,0,1): (u=>u*u*(3-2*u))(clamp(t,0,1));
  const lerp=(a,b)=>Number(a)+(Number(b)-Number(a))*easing;
  let position=from.map((v,i)=>lerp(v,to[i]));
  if(settings?.mode==='orbit'){
    const center=target,angle=(Number(settings.angle)||35)*Math.PI/180*easing;
    const dx=position[0]-center[0],dz=position[2]-center[2];
    position=[center[0]+dx*Math.cos(angle)-dz*Math.sin(angle),position[1],
              center[2]+dx*Math.sin(angle)+dz*Math.cos(angle)];
  }
  return {position,target:target.map(Number)};
}
export function streetSegments(world){
  const paths=world.roads?.paths;
  return Array.isArray(paths)?paths.filter(p=>Array.isArray(p)&&p.length>1):[];
}
export function buildingActive(b,year){return b.from<=year&&(b.to==null||year<b.to);}
