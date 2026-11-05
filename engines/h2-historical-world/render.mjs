// H2 Historical World CLI renderer compatible with BanDoSu Studio.
// Usage: node render.mjs --project <dir> [--check|--sheet|--thumb|--from 0 --to 30] [--scale .5]
import fs from 'node:fs';
import http from 'node:http';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawn} from 'node:child_process';
import {once} from 'node:events';
import {chromium} from 'playwright';

const root=path.dirname(fileURLToPath(import.meta.url));
const argv=process.argv.slice(2);
const option=(name,fallback)=>{const i=argv.indexOf(name);return i<0?fallback:argv[i+1]};
const flag=(name)=>argv.includes(name);
const candidate=option('--project','');
if(!candidate)throw new Error('Use --project <directory>');
const project=path.resolve(root,candidate);
for(const f of ['config.json','story/shots.json','history/world.json'])
  if(!fs.existsSync(path.join(project,f)))throw new Error('Missing project file: '+f);
const safeResolve=(prefix,pathname)=>{
  let rel;
  try{rel=decodeURIComponent(pathname)}catch{return null}
  if(rel.includes('\0')||rel.includes('\\'))return null;
  const abs=path.resolve(prefix,'.'+rel);
  return abs===prefix||abs.startsWith(prefix+path.sep)?abs:null;
};
const mime={'.js':'text/javascript','.mjs':'text/javascript','.html':'text/html',
  '.json':'application/json','.png':'image/png','.jpg':'image/jpeg','.css':'text/css'};
const server=http.createServer((req,res)=>{
  try{
    const request=new URL(req.url,'http://127.0.0.1');
    const isProj=request.pathname.startsWith('/proj/');
    const base=isProj?project:root;
    const name=isProj?request.pathname.slice(5):request.pathname;
    const file=safeResolve(base,name);
    if(!file){res.writeHead(403);return res.end('Forbidden')}
    if(!fs.existsSync(file)||!fs.statSync(file).isFile()){res.writeHead(404);return res.end('Not found')}
    res.writeHead(200,{'content-type':mime[path.extname(file)]||'application/octet-stream',
      'cache-control':'no-store','x-content-type-options':'nosniff'});
    fs.createReadStream(file).pipe(res);
  }catch(e){res.writeHead(400);res.end(String(e))}
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const baseUrl='http://127.0.0.1:'+server.address().port;
const scale=Number(option('--scale','1'));
if(!Number.isFinite(scale)||scale<=0||scale>1)throw new Error('--scale must be (0,1]');
const browser=await chromium.launch({headless:true,args:['--disable-dev-shm-usage']});
const page=await browser.newPage({viewport:{width:1920,height:1080},deviceScaleFactor:1});
page.on('pageerror',e=>console.error('[browser]',e));
page.on('console',message=>{if(message.type()==='error')console.error('[browser]',message.text())});
page.on('response',res=>{if(res.status()>=400)console.error('[HTTP '+res.status()+']',res.url())});
const ensureBuild=()=>{const d=path.join(project,'build');fs.mkdirSync(d,{recursive:true});return d};
const draw=frame=>page.evaluate(f=>window.renderFrame(f),frame);
async function openPlayer(){
  await page.goto(baseUrl+'/player.html?proj=/proj&scale='+scale);
  await page.waitForFunction(()=>window.__ready||window.__bootError,null,{timeout:90000});
  const error=await page.evaluate(()=>window.__bootError||null);
  if(error)throw new Error('Player boot failed: '+error);
  return await page.evaluate(()=>window.__info);
}
async function generateVideo(info,start,end,out){
  if(!Number.isInteger(start)||!Number.isInteger(end)||end<=start)throw new Error('Invalid frame window');
  const ffmpeg=spawn('ffmpeg',['-hide_banner','-loglevel','error','-y',
    '-f','image2pipe','-vcodec','png','-framerate',String(info.fps),'-i','pipe:0',
    '-an','-c:v','libx264','-pix_fmt','yuv420p','-crf','20','-preset','medium',
    '-movflags','+faststart',out],{stdio:['pipe','inherit','inherit']});
  let stdinFailure=null;
  ffmpeg.stdin.on('error',err=>{stdinFailure=err});
  try {
    for(let frame=start;frame<end;frame++){
      if(stdinFailure)throw stdinFailure;
      await draw(frame);
      const png=await page.screenshot({type:'png'});
      if(!ffmpeg.stdin.write(png))await once(ffmpeg.stdin,'drain');
      if(frame%Math.max(1,Math.round(info.fps*5))===0)
        console.log('Render '+(frame-start)+'/'+(end-start));
    }
    ffmpeg.stdin.end();
    const [exit]=await once(ffmpeg,'close');
    if(exit!==0)throw new Error('FFmpeg failed: '+exit);
    if(!fs.existsSync(out)||fs.statSync(out).size<1024)
      throw new Error('FFmpeg produced no valid output');
    console.log('Xong: '+out);
  }catch(e){
    ffmpeg.stdin.destroy();
    if(ffmpeg.exitCode===null)ffmpeg.kill();
    throw e;
  }
}
async function sheet(info){
  const story=JSON.parse(fs.readFileSync(path.join(project,'story','shots.json'),'utf8'));
  const shots=story.shots;
  const dest=path.join(ensureBuild(),'sheets');fs.mkdirSync(dest,{recursive:true});
  // Compose each sheet in browser Canvas, preserving labels and source timestamps.
  const width=460,height=Math.round(width*info.H/info.W),columns=4,rows=2;
  const perPage=columns*rows;
  for(let pageId=0;pageId<Math.ceil(shots.length/perPage);pageId++){
    const frames=[];
    for(const shot of shots.slice(pageId*perPage,(pageId+1)*perPage)){
      const sec=shot.start+.85*(shot.end-shot.start);
      await draw(Math.min(info.totalFrames-1,Math.round(sec*info.fps)));
      frames.push({png:(await page.screenshot({type:'png'})).toString('base64'),
        title:shot.id+' '+(shot.line||'').slice(0,55)});
    }
    const jpeg=await page.evaluate(async({frames,width,height,columns,rows})=>{
      const canvas=document.createElement('canvas');
      canvas.width=width*columns;canvas.height=(height+26)*rows;
      const ctx=canvas.getContext('2d');ctx.fillStyle='#111';ctx.fillRect(0,0,canvas.width,canvas.height);
      for(let i=0;i<frames.length;i++){
        const img=new Image();
        img.src='data:image/png;base64,'+frames[i].png;
        await img.decode();
        const x=(i%columns)*width,y=Math.floor(i/columns)*(height+26);
        ctx.drawImage(img,x,y,width,height);
        ctx.fillStyle='#eee';ctx.font='15px sans-serif';
        ctx.fillText(frames[i].title,x+4,y+height+18,width-8);
      }
      return canvas.toDataURL('image/jpeg',.88).split(',')[1];
    },{frames,width,height,columns,rows});
    const file=path.join(dest,'sheet_'+String(pageId+1).padStart(2,'0')+'.jpg');
    fs.writeFileSync(file,Buffer.from(jpeg,'base64'));console.log('Sheet: '+file);
  }
}
try{
  const info=await openPlayer();
  await page.setViewportSize({width:info.W,height:info.H});
  if(flag('--check')){
    const story=JSON.parse(fs.readFileSync(path.join(project,'story','shots.json'),'utf8'));
    for(const shot of story.shots){
      await draw(Math.round(shot.start*info.fps));
      console.log('OK '+shot.id+' epoch='+shot.args?.epoch);
    }
  }else if(flag('--sheet'))await sheet(info);
  else if(flag('--thumb')){
    await draw(0);
    const target=path.join(ensureBuild(),'thumbnail.png');
    await page.screenshot({path:target,type:'png'});console.log('Thumbnail: '+target);
  }else{
    const from=Math.max(0,Number(option('--from','0')));
    const to=Math.min(info.duration,Number(option('--to',String(info.duration))));
    if(!Number.isFinite(from)||!Number.isFinite(to)||to<=from)throw new Error('Invalid --from/--to');
    const start=Math.max(0,Math.floor(from*info.fps)),end=Math.min(info.totalFrames,Math.ceil(to*info.fps));
    const out=path.join(ensureBuild(),flag('--from')||flag('--to')?'preview.mp4':'video_silent.mp4');
    await generateVideo(info,start,end,out);
  }
}finally{await page.close();await browser.close();await new Promise(resolve=>server.close(resolve))}
