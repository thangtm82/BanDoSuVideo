// Pure deterministic road planning functions shared by renderer and tests.
// V2 array roads remain valid. V3.5 object roads support kind and build year.
export function normalizeRoads(world) {
  return (world.roads?.paths||[]).filter(p=>Array.isArray(p)?p.length>1:
    p&&Array.isArray(p.pts)&&p.pts.length>1).map((r,i)=>Array.isArray(r)?
    {id:'legacy-'+i,pts:r,kind:'secondary',from:-Infinity}:{
    id:r.id||'road-'+i,pts:r.pts,kind:r.kind||'secondary',
    from:Number.isFinite(r.from)?r.from:-Infinity,to:Number.isFinite(r.to)?r.to:Infinity});
}
export function distanceToRiver(world,x,z){
  const pts=world.river?.pts||[];let dist=Infinity;
  for(let i=1;i<pts.length;i++){
    const [ax,az]=pts[i-1],[bx,bz]=pts[i],dx=bx-ax,dz=bz-az;
    const len2=dx*dx+dz*dz;
    const t=len2?Math.max(0,Math.min(1,((x-ax)*dx+(z-az)*dz)/len2)):0;
    dist=Math.min(dist,Math.hypot(x-ax-t*dx,z-az-t*dz));
  }
  return dist;
}
export function roadPieces(world,road,step=5){
  const segments=[];let piece=[];
  const shore=Math.max(2,Number(world.river?.width)||12)+3;
  const flush=()=>{if(piece.length>1)segments.push(piece);piece=[]};
  for(let i=1;i<road.pts.length;i++){
    const [ax,az]=road.pts[i-1],[bx,bz]=road.pts[i];
    const n=Math.max(1,Math.ceil(Math.hypot(bx-ax,bz-az)/step));
    for(let j=(i===1?0:1);j<=n;j++){
      const t=j/n,x=ax+(bx-ax)*t,z=az+(bz-az)*t;
      if(distanceToRiver(world,x,z)<=shore){flush();continue;}
      piece.push([x,z]);
    }
  }
  flush();
  return segments;
}
export function roadVisible(road,year){return road.from<=year&&(road.to==null||year<road.to);}
