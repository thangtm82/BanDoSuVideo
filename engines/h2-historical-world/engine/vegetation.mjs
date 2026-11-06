// Repeatable vegetation patches. This is illustrative ecology, not GIS data.
export function canopyScale(world,x,z,noise){
  const city=Math.hypot(x,z);
  const river=world.river?.pts||[];
  let gap=Infinity;
  for(let i=1;i<river.length;i++){
    const [ax,az]=river[i-1],[bx,bz]=river[i];
    const dx=bx-ax,dz=bz-az,d=dx*dx+dz*dz;
    const t=d?Math.max(0,Math.min(1,((x-ax)*dx+(z-az)*dz)/d)):0;
    gap=Math.min(gap,Math.hypot(x-ax-t*dx,z-az-t*dz));
  }
  const bank=Math.max(12,Number(world.river?.width)||12)*1.55;
  if(city<220 || gap<bank)return 0;
  const patch=.55+.28*Math.sin(x*.012+z*.004)*Math.cos(z*.015-x*.006);
  if(noise>patch)return 0;
  const riverEdge=Number.isFinite(gap)&&gap<bank+30;
  return (.57+.7*(1-noise))*(riverEdge?.82:1);
}
export function canopyTint(x,z,noise){
  const variation=Math.sin(x*.023+z*.017)*.08+(noise-.5)*.13;
  return Math.max(0,Math.min(1,.42+variation));
}
