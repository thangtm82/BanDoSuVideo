import * as THREE from '/node_modules/three/build/three.module.js';
import {activeBuildings,layout,shotAtTime,shotT,validateHistory} from './timeline.mjs';

const clamp=(n,min,max)=>Math.max(min,Math.min(max,n));
const color=(value,fallback)=>{try{return new THREE.Color(value||fallback)}catch{return new THREE.Color(fallback)}};

function buildTerrain(scene,world){
  const size=Number(world.size)||1600;
  const terrain=new THREE.PlaneGeometry(size,size,96,96);
  terrain.rotateX(-Math.PI/2);
  const position=terrain.attributes.position;
  const hills=Array.isArray(world.hills)?world.hills:[];
  for(let i=0;i<position.count;i++){
    const x=position.getX(i),z=position.getZ(i);
    let h=1.5*Math.sin(x*.012)*Math.cos(z*.017);
    for(const hill of hills){
      const hx=Number(hill.p?.[0])||0,hz=Number(hill.p?.[1])||0;
      const r=Math.max(5,Number(hill.r)||100);
      const d=((x-hx)**2+(z-hz)**2)/(r*r);
      h+=Math.exp(-2*d)*(Number(hill.h)||45);
    }
    position.setY(i,h);
  }
  terrain.computeVertexNormals();
  const ground=new THREE.Mesh(terrain,new THREE.MeshStandardMaterial({
    color:color(world.ground,'#a9a48a'),roughness:1,metalness:0
  }));
  ground.receiveShadow=true;scene.add(ground);
  const river=world.river;
  if(Array.isArray(river?.pts)&&river.pts.length>1){
    const curve=new THREE.CatmullRomCurve3(river.pts.map(p=>new THREE.Vector3(p[0],2.5,p[1])));
    const mesh=new THREE.Mesh(new THREE.TubeGeometry(curve,128,Number(river.width)||16,8,false),
      new THREE.MeshStandardMaterial({color:'#367d9d',roughness:.4,metalness:.04}));
    scene.add(mesh);
  }
  // Repeatable instanced trees use local coordinates; no per-frame randomness.
  const vegetation=world.vegetation||{};
  const trees=Math.min(10000,Math.max(0,Number(vegetation.count)||0));
  if(trees){
    const trunk=new THREE.CylinderGeometry(1.2,1.6,9,5);
    const crown=new THREE.ConeGeometry(5,17,6);
    const trunkMesh=new THREE.InstancedMesh(trunk,new THREE.MeshStandardMaterial({color:'#59442f'}),trees);
    const foliage=new THREE.InstancedMesh(crown,new THREE.MeshStandardMaterial({color:'#497c4c',roughness:1}),trees);
    const dummy=new THREE.Object3D();
    const positions=layout(Number(vegetation.seed)||31,trees,size*.46);
    for(let i=0;i<trees;i++){
      const [x,z,r]=positions[i];
      const scale=.65+r*.8;
      const land=terrainHeight(world,x,z);
      dummy.position.set(x,land+4.5*scale,z);dummy.scale.setScalar(scale);dummy.rotation.set(0,0,0);
      dummy.updateMatrix();trunkMesh.setMatrixAt(i,dummy.matrix);
      dummy.position.y=land+16*scale;dummy.updateMatrix();foliage.setMatrixAt(i,dummy.matrix);
    }
    trunkMesh.instanceMatrix.needsUpdate=true;foliage.instanceMatrix.needsUpdate=true;
    trunkMesh.castShadow=false;foliage.castShadow=false;
    scene.add(trunkMesh,foliage);
  }
}
function terrainHeight(world,x,z){
  let h=1.5*Math.sin(x*.012)*Math.cos(z*.017);
  for(const hill of world.hills||[]){
    const r=Math.max(5,Number(hill.r)||100),p=hill.p||[0,0];
    h+=Math.exp(-2*((x-p[0])**2+(z-p[1])**2)/(r*r))*(Number(hill.h)||45);
  }
  return h;
}
function buildingGeometry(kind){
  if(kind==='tower')return new THREE.CylinderGeometry(9,11,32,8);
  if(kind==='wall')return new THREE.BoxGeometry(32,20,8);
  return new THREE.BoxGeometry(15,14,18);
}
export class HistoricalWorldRenderer{
  constructor(canvas,config,history,story){
    validateHistory(history);
    this.config=config;this.history=history;this.story=story;
    this.world=history.world||{};
    this.renderer=new THREE.WebGLRenderer({canvas,antialias:true,preserveDrawingBuffer:true});
    this.renderer.setPixelRatio(1);
    this.renderer.setSize(config.width||1280,config.height||720,false);
    this.renderer.outputColorSpace=THREE.SRGBColorSpace;
    this.renderer.toneMapping=THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure=1.25;
    this.scene=new THREE.Scene();this.scene.background=color(this.world.sky,'#c4d8d4');
    this.scene.fog=new THREE.Fog(this.scene.background,2000,4500);
    this.scene.add(new THREE.HemisphereLight('#e6f1ff','#887451',2.2));
    const light=new THREE.DirectionalLight('#fff0d5',2.6);
    light.position.set(-300,700,280);this.scene.add(light);
    buildTerrain(this.scene,this.world);
    this.camera=new THREE.PerspectiveCamera(42,(config.width||1280)/(config.height||720),1,6000);
    this.buildings=this.createBuildings();
  }
  createBuildings(){
    // Group by material and type, then keep individual visibility in epoch selection.
    const instances=[];
    const groups=new Map();
    for(const b of this.history.buildings||[]){
      const key=(b.kind||'house')+':'+(b.color||'#c7a17e');
      if(!groups.has(key))groups.set(key,[]);
      groups.get(key).push(b);
    }
    for(const [key,buildings] of groups){
      const [kind,tint]=key.split(':');
      const mesh=new THREE.InstancedMesh(buildingGeometry(kind),
        new THREE.MeshStandardMaterial({color:tint,roughness:.91}),buildings.length);
      mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
      this.scene.add(mesh);
      instances.push({mesh,buildings});
    }
    return instances;
  }
  renderFrame(frame){
    const fps=this.config.fps||24;
    const seconds=Math.max(0,Number(frame)||0)/fps;
    const shot=shotAtTime(this.story,seconds);
    const epoch=shot.args?.epoch||this.history.epochs[0].id;
    const year=this.history.epochs.find(e=>e.id===epoch)?.year;
    if(year===undefined)throw new Error('Shot references unknown epoch: '+epoch);
    for(const {mesh,buildings} of this.buildings){
      const dummy=new THREE.Object3D();
      buildings.forEach((b,i)=>{
        const live=b.from<=year&&(b.to==null||year<b.to);
        const [x,z]=b.p;
        dummy.position.set(x,terrainHeight(this.world,x,z)+(b.kind==='tower'?16:b.kind==='wall'?10:7),z);
        dummy.rotation.set(0,Number(b.rotation)||0,0);
        dummy.scale.setScalar(live?(Number(b.scale)||1):0);
        dummy.updateMatrix();mesh.setMatrixAt(i,dummy.matrix);
      });
      mesh.instanceMatrix.needsUpdate=true;mesh.computeBoundingSphere();
    }
    const t=shotT(shot,seconds),settings=shot.args?.camera||{};
    const start=settings.from||[620,650,760],end=settings.to||start;
    const at=settings.target||[0,0,0];
    this.camera.position.set(
      THREE.MathUtils.lerp(Number(start[0]),Number(end[0]),t),
      THREE.MathUtils.lerp(Number(start[1]),Number(end[1]),t),
      THREE.MathUtils.lerp(Number(start[2]),Number(end[2]),t));
    this.camera.lookAt(Number(at[0]),Number(at[1]),Number(at[2]));
    this.renderer.render(this.scene,this.camera);
    return {shot:shot.id,epoch,seconds};
  }
  dispose(){this.renderer.dispose();}
}
