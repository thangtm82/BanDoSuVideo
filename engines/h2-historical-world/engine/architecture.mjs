// Procedural architectural parts share a single per-variant instancing group.
export const HOUSE_VARIANTS=['gable','hip','courtyard','longhouse','workshop','storehouse','two-storey','terraced'];
export function variantFor(b){
  if(b.kind!=='house')return b.kind||'house';
  if(HOUSE_VARIANTS.includes(b.variant))return b.variant;
  let h=2166136261;
  for(const c of String(b.id||''))h=Math.imul(h^c.charCodeAt(0),16777619);
  return HOUSE_VARIANTS[(h>>>0)%HOUSE_VARIANTS.length];
}
export function buildingParts(T,kind,variant,tint){
  const parts=[],wall=tint||'#c7a17e',roof='#916c55';
  const add=(geometry,color,offset)=>parts.push({geometry,color,offset});
  const box=(a,b,c)=>new T.BoxGeometry(a,b,c);
  const hip=(r,h)=>{const g=new T.ConeGeometry(r,h,4);g.rotateY(Math.PI/4);return g;};
  if(kind==='wall'){
    add(box(32,17,8),'#9d927e',[0,8.5,0]);
    add(box(34,2,10),'#b1a38a',[0,18,0]);
    for(const x of [-13,-7,0,7,13])add(box(3,3,10),'#b1a38a',[x,20.5,0]);
  }else if(kind==='gate'){
    for(const x of [-12,12])add(box(8,24,11),'#a69b84',[x,12,0]);
    add(box(32,6,12),'#aa9e86',[0,23,0]);
    add(hip(18,7),'#89654e',[0,29,0]);
  }else if(kind==='tower'){
    add(new T.CylinderGeometry(10,11.5,31,8),'#9e927d',[0,15.5,0]);
    add(new T.CylinderGeometry(12,12,2,8),'#b4a58b',[0,32,0]);
    add(new T.ConeGeometry(13,12,8),'#795545',[0,39,0]);
  }else if(kind==='temple'||kind==='market'){
    const ceremonial=kind==='temple';
    add(box(ceremonial?28:27,ceremonial?22:12,ceremonial?34:24),ceremonial?'#c2b59a':'#b8a081',[0,ceremonial?11:6,0]);
    add(hip(ceremonial?25:21,ceremonial?15:10),ceremonial?'#776550':'#9a6d4f',[0,ceremonial?29:17,0]);
    add(box(8,9,6),'#80654b',[0,4.5,ceremonial?19:14]);
  }else{
    const taller=variant==='two-storey'||variant==='terraced';
    const w=variant==='longhouse'?24:variant==='workshop'||variant==='courtyard'?19:15;
    const d=variant==='storehouse'?24:variant==='courtyard'?21:18;
    const h=taller?22:variant==='storehouse'?17:13;
    add(box(w,h,d),wall,[0,h/2,0]);
    if(variant==='terraced'){
      add(box(17,2,19),'#8b5e49',[0,h+1,0]);
      add(box(7,5,8),'#aa937b',[0,h+4,0]);
    }else{
      add(hip(variant==='storehouse'?17:variant==='hip'?15:variant==='longhouse'?16:14,variant==='hip'?11:9),
        variant==='workshop'?'#76604c':variant==='storehouse'?'#857155':roof,[0,h+4.5,0]);
    }
    if(variant==='courtyard')add(box(7,10,8),wall,[w*.56,5,-d*.25]);
    if(variant==='workshop')add(box(4,12,4),'#b1a38e',[w*.2,h+7,0]);
    add(box(4,7,.6),'#69503e',[0,3.5,d/2+.35]);
    if(taller)for(const x of [-4,4])add(box(2.5,3,.6),'#595d57',[x,h*.65,d/2+.4]);
  }
  return parts;
}
