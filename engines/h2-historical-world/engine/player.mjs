import {HistoricalWorldRenderer} from './world.mjs';
let visual;
const load=async url=>{
  const response=await fetch(url,{cache:'no-store'});
  if(!response.ok)throw new Error('HTTP '+response.status+' '+url);
  return response.json();
};
try {
  const params=new URLSearchParams(location.search);
  const base=params.get('proj')||'/proj';
  const scale=Math.max(.1,Math.min(1,Number(params.get('scale')||1)));
  const [config,history,story]=await Promise.all([
    load(base+'/config.json'),load(base+'/history/world.json'),load(base+'/story/shots.json')
  ]);
  if(!Array.isArray(story.shots)||!story.shots.length)throw new Error('No scenes in story/shots.json');
  const fps=Number(config.fps)||24;
  const duration=Math.max(...story.shots.map(s=>Number(s.end)));
  if(!Number.isFinite(duration)||duration<=0)throw new Error('Invalid timeline duration');
  const dimensions={...config,width:Math.max(64,Math.round((config.width||1280)*scale)),
    height:Math.max(64,Math.round((config.height||720)*scale))};
  const stage=document.getElementById('stage');
  stage.width=dimensions.width;stage.height=dimensions.height;
  visual=new HistoricalWorldRenderer(stage,dimensions,history,story);
  window.__info={W:dimensions.width,H:dimensions.height,fps,totalFrames:Math.ceil(duration*fps),duration};
  window.renderFrame=(frame)=>visual.renderFrame(frame);
  window.renderFrame(0);
  window.__ready=true;
} catch(error) {
  window.__bootError=error?.stack||String(error);
  const element=document.getElementById('error');
  element.textContent=String(window.__bootError);element.style.display='block';
  console.error(error);
}
