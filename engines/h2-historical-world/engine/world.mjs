import * as THREE from '/node_modules/three/build/three.module.js';
import {layout,shotAtTime,shotT,validateHistory} from './timeline.mjs';
import {surfaceHeight,nearestRiverDistance,cameraPose,streetSegments,buildingActive} from './visuals.mjs';

const clamp=(n,min,max)=>Math.max(min,Math.min(max,n));
const color=(value,fallback)=>{try{return new THREE.Color(value||fallback)}catch{return new THREE.Color(fallback)}};

function buildTerrain(scene,world){
  const size=Number(world.size)||1600;
  const terrain=new THREE.PlaneGeometry(size,size,192,192);
  terrain.rotateX(-Math.PI/2);
  const position=terrain.attributes.position;
  const colors=[];
  const base=color(world.ground,'#a9a48a'), grass=new THREE.Color('#738a60');
  for(let i=0;i<position.count;i++){
    const x=position.getX(i),z=position.getZ(i);
    const y=surfaceHeight(world,x,z);
    position.setY(i,y);
    const n=(Math.sin(x*.071)*Math.cos(z*.093)+Math.sin(z*.035+x*.021))*.08;
    const tint=base.clone().lerp(grass,clamp(.22+n+(y>45?.18:0),0,.7));
    colors.push(tint.r,tint.g,tint.b);
  }
  terrain.setAttribute('color',new THREE.Float32BufferAttribute(colors,3));
  terrain.computeVertexNormals();
  const ground=new THREE.Mesh(terrain,new THREE.MeshStandardMaterial({
    vertexColors:true,roughness:1,metalness:0,side:THREE.DoubleSide
  }));
  ground.receiveShadow=true;scene.add(ground);

  // Flat ribbon water instead of the V1 round blue tube.
  const river=world.river;
  if(Array.isArray(river?.pts)&&river.pts.length>1){
    const curve=new THREE.CatmullRomCurve3(river.pts.map(p=>new THREE.Vector3(p[0],0,p[1])));
    const segments=180,halfWidth=Math.max(2,Number(river.width)||12);
    const vertices=[],uv=[],indices=[];
    for(let i=0;i<=segments;i++){
      const t=i/segments,p=curve.getPoint(t),d=curve.getTangent(t);
      const length=Math.hypot(d.x,d.z)||1,nx=-d.z/length,nz=d.x/length;
      for(const side of [-1,1]){
        const x=p.x+side*nx*halfWidth,z=p.z+side*nz*halfWidth;
        vertices.push(x,surfaceHeight(world,x,z)+.8,z);
        uv.push(t*18,(side+1)/2);
      }
      if(i<segments){const j=i*2;indices.push(j,j+2,j+1,j+1,j+2,j+3)}
    }
    const g=new THREE.BufferGeometry();
    g.setAttribute('position',new THREE.Float32BufferAttribute(vertices,3));
    g.setAttribute('uv',new THREE.Float32BufferAttribute(uv,2));
    g.setIndex(indices);g.computeVertexNormals();
    const water=new THREE.Mesh(g,new THREE.MeshStandardMaterial({
      color:color(river.color,'#367f91'),metalness:.12,roughness:.24,
      transparent:true,opacity:.93,side:THREE.DoubleSide
    }));
    water.receiveShadow=true;scene.add(water);
  }

  // Roads follow the existing terrain height. They are optional to preserve V1 data.
  for(const path of streetSegments(world)){
    const points=path.map(([x,z])=>new THREE.Vector3(x,surfaceHeight(world,x,z)+.8,z));
    const road=new THREE.Mesh(new THREE.TubeGeometry(new THREE.CatmullRomCurve3(points),Math.max(16,path.length*10),2.1,4,false),
      new THREE.MeshStandardMaterial({color:'#8c8068',roughness:1}));
    scene.add(road);
  }

  const vegetation=world.vegetation||{};
  const trees=Math.min(10000,Math.max(0,Number(vegetation.count)||0));
  if(trees){
    const trunk=new THREE.CylinderGeometry(1.0,1.45,9,6);
    const crown=new THREE.ConeGeometry(5.5,16,7);
    const crown2=new THREE.ConeGeometry(4.1,12,7);
    const trunkMesh=new THREE.InstancedMesh(trunk,new THREE.MeshStandardMaterial({color:'#614a30'}),trees);
    const foliage=new THREE.InstancedMesh(crown,new THREE.MeshStandardMaterial({color:'#456f48',roughness:1}),trees);
    const foliage2=new THREE.InstancedMesh(crown2,new THREE.MeshStandardMaterial({color:'#5a8951',roughness:1}),trees);
    const dummy=new THREE.Object3D(),positions=layout(Number(vegetation.seed)||31,trees,size*.46);
    for(let i=0;i<trees;i++){
      const [x,z,r]=positions[i],scale=.65+r*.8;
      const riverGap=nearestRiverDistance(world,x,z);
      // Instanced zero-scale slots retain deterministic placement and counts.
      const hidden=riverGap<Math.max(12,Number(world.river?.width)||12)*1.7 || Math.hypot(x,z)<235;
      const k=hidden?0:scale,land=surfaceHeight(world,x,z);
      dummy.position.set(x,land+4.5*k,z);dummy.scale.setScalar(k);dummy.rotation.set(0,0,0);
      dummy.updateMatrix();trunkMesh.setMatrixAt(i,dummy.matrix);
      dummy.position.y=land+14*k;dummy.updateMatrix();foliage.setMatrixAt(i,dummy.matrix);
      dummy.position.y=land+21*k;dummy.updateMatrix();foliage2.setMatrixAt(i,dummy.matrix);
    }
    for(const mesh of [trunkMesh,foliage,foliage2]){
      mesh.instanceMatrix.needsUpdate=true;
      mesh.castShadow=true;mesh.receiveShadow=true;scene.add(mesh);
    }
  }
}
function buildingGeometry(kind){
  if(kind==='tower')return new THREE.CylinderGeometry(9,11,32,8);
  if(kind==='wall')return new THREE.BoxGeometry(32,20,8);
  return new THREE.BoxGeometry(15,14,18);
}
function roofGeometry(kind){
  if(kind==='wall')return new THREE.BoxGeometry(34,2.5,11);
  if(kind==='tower')return new THREE.ConeGeometry(13,12,8);
  // Rotate the roof ridge along local Z on buildings.
  const roof=new THREE.CylinderGeometry(0,10,17,4,1);
  roof.rotateZ(Math.PI/2);roof.rotateY(Math.PI/4);
  return roof;
}
export class HistoricalWorldRenderer{
  constructor(canvas,config,history,story){
    validateHistory(history);
    this.config=config;this.history=history;this.story=story;
    this.world=history.world||{};
    this.renderer=new THREE.WebGLRenderer({canvas,antialias:true,preserveDrawingBuffer:true});
    this.renderer.setPixelRatio(1);
    this.renderer.shadowMap.enabled=true;
    this.renderer.shadowMap.type=THREE.PCFSoftShadowMap;
    this.renderer.setSize(config.width||1280,config.height||720,false);
    this.renderer.outputColorSpace=THREE.SRGBColorSpace;
    this.renderer.toneMapping=THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure=Number(this.world.exposure)||1.12;
    this.scene=new THREE.Scene();this.scene.background=color(this.world.sky,'#c4d8d4');
    this.scene.fog=new THREE.FogExp2(this.scene.background,.00030);
    this.scene.add(new THREE.HemisphereLight('#d6ecff','#857258',1.65));
    const light=new THREE.DirectionalLight('#fff0d5',2.6);
    light.position.set(-300,700,280);
    light.castShadow=true;light.shadow.mapSize.set(2048,2048);
    light.shadow.camera.left=-900;light.shadow.camera.right=900;
    light.shadow.camera.top=900;light.shadow.camera.bottom=-900;
    light.shadow.camera.near=1;light.shadow.camera.far=2000;
    light.shadow.bias=-.0004;this.scene.add(light);
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
      mesh.castShadow=true;mesh.receiveShadow=true;
      const roof=new THREE.InstancedMesh(roofGeometry(kind),
        new THREE.MeshStandardMaterial({color:kind==='tower'?'#825c49':kind==='wall'?'#ab997b':'#855947',roughness:.92}),
        buildings.length);
      roof.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
      roof.castShadow=true;roof.receiveShadow=true;
      this.scene.add(mesh,roof);
      instances.push({mesh,roof,buildings,kind});
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
    for(const {mesh,roof,buildings,kind} of this.buildings){
      const dummy=new THREE.Object3D();
      buildings.forEach((b,i)=>{
        const live=buildingActive(b,year);
        const [x,z]=b.p;
        const ground=surfaceHeight(this.world,x,z);
        dummy.position.set(x,ground+(kind==='tower'?16:kind==='wall'?10:7),z);
        dummy.rotation.set(0,Number(b.rotation)||0,0);
        dummy.scale.setScalar(live?(Number(b.scale)||1):0);
        dummy.updateMatrix();mesh.setMatrixAt(i,dummy.matrix);
        dummy.position.y=ground+(kind==='tower'?37:kind==='wall'?21:19)*(Number(b.scale)||1);
        dummy.updateMatrix();roof.setMatrixAt(i,dummy.matrix);
      });
      mesh.instanceMatrix.needsUpdate=true;mesh.computeBoundingSphere();
      roof.instanceMatrix.needsUpdate=true;roof.computeBoundingSphere();
    }
    const t=shotT(shot,seconds),settings=shot.args?.camera||{};
    const pose=cameraPose(settings,t);
    this.camera.position.set(...pose.position);
    this.camera.lookAt(...pose.target);
    this.renderer.render(this.scene,this.camera);
    return {shot:shot.id,epoch,seconds};
  }
  dispose(){this.renderer.dispose();}
}
