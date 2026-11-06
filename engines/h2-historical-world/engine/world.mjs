import * as THREE from '/node_modules/three/build/three.module.js';
import {layout,shotAtTime,shotT,validateHistory} from './timeline.mjs';
import {surfaceHeight,nearestRiverDistance,cameraPose,streetSegments} from './visuals.mjs';
import {visibleAt,validateEvents} from './history-events.mjs';
import {normalizeRoads,roadPieces,roadVisible} from './roads.mjs';
import {variantFor,buildingParts} from './architecture.mjs';

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

  // Roads are shallow ribbons, not raised round tubes. River-crossing portions
  // are clipped unless a future bridge asset is explicitly modeled.
  const roadMeshes=[];
  for(const road of normalizeRoads(world)){
    const group=new THREE.Group();
    for(const path of roadPieces(world,road)){
      const width=road.kind==='primary'?2.8:road.kind==='alley'?1.05:1.7;
      const verts=[],indices=[];
      for(let i=0;i<path.length;i++){
        const [x,z]=path[i],prev=path[Math.max(0,i-1)],next=path[Math.min(path.length-1,i+1)];
        const dx=next[0]-prev[0],dz=next[1]-prev[1],len=Math.hypot(dx,dz)||1;
        const nx=-dz/len,nz=dx/len;
        for(const side of [-1,1]){
          const px=x+nx*side*width,pz=z+nz*side*width;
          verts.push(px,surfaceHeight(world,px,pz)+.12,pz);
        }
        if(i<path.length-1){const j=i*2;indices.push(j,j+2,j+1,j+1,j+2,j+3)}
      }
      const g=new THREE.BufferGeometry();
      g.setAttribute('position',new THREE.Float32BufferAttribute(verts,3));
      g.setIndex(indices);g.computeVertexNormals();
      const mesh=new THREE.Mesh(g,new THREE.MeshStandardMaterial({
        color:road.kind==='primary'?'#a89e87':'#aca58f',
        roughness:1,transparent:true,opacity:.62,
        depthWrite:false,side:THREE.DoubleSide,polygonOffset:true,
        polygonOffsetFactor:-1,polygonOffsetUnits:-1
      }));
      mesh.castShadow=false;mesh.receiveShadow=true;group.add(mesh);
    }
    scene.add(group);roadMeshes.push({road,group});
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
  return roadMeshes;
}
export class HistoricalWorldRenderer{
  constructor(canvas,config,history,story){
    validateHistory(history);
    validateEvents(history);
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
    this.roadMeshes=buildTerrain(this.scene,this.world);
    this.camera=new THREE.PerspectiveCamera(42,(config.width||1280)/(config.height||720),1,6000);
    this.buildings=this.createBuildings();
  }
  createBuildings(){
    // Instance each architectural piece by variant + material; stable lifetime
    // changes never reallocate geometry or change any historical object ID.
    const instances=[],groups=new Map();
    for(const building of this.history.buildings||[]){
      const kind=building.kind||'house',variant=variantFor(building);
      const tint=building.color||'#c7a17e';
      const key=[kind,variant,tint].join('|');
      if(!groups.has(key))groups.set(key,{kind,variant,tint,buildings:[]});
      groups.get(key).buildings.push(building);
    }
    for(const {kind,variant,tint,buildings} of groups.values()){
      const pieces=buildingParts(THREE,kind,variant,tint).map(part=>{
        const mesh=new THREE.InstancedMesh(part.geometry,
          new THREE.MeshStandardMaterial({color:part.color,roughness:.89}),
          buildings.length);
        mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
        mesh.castShadow=true;mesh.receiveShadow=true;
        this.scene.add(mesh);
        return {mesh,offset:new THREE.Vector3(...part.offset)};
      });
      instances.push({buildings,pieces});
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
    for(const {road,group} of this.roadMeshes)group.visible=roadVisible(road,year);
    const base=new THREE.Object3D(),offsetMatrix=new THREE.Matrix4(),finalMatrix=new THREE.Matrix4();
    for(const {pieces,buildings} of this.buildings){
      for(let i=0;i<buildings.length;i++){
        const building=buildings[i],live=visibleAt(this.history,building,year);
        const [x,z]=building.p;
        base.position.set(x,surfaceHeight(this.world,x,z),z);
        base.rotation.set(0,Number(building.rotation)||0,0);
        base.scale.setScalar(live?(Number(building.scale)||1):0);
        base.updateMatrix();
        for(const part of pieces){
          offsetMatrix.makeTranslation(part.offset.x,part.offset.y,part.offset.z);
          finalMatrix.multiplyMatrices(base.matrix,offsetMatrix);
          part.mesh.setMatrixAt(i,finalMatrix);
        }
      }
      for(const part of pieces){
        part.mesh.instanceMatrix.needsUpdate=true;
        part.mesh.computeBoundingSphere();
      }
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
