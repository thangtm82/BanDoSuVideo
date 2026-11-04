// Persistent read-only preview server for the original H2 Canvas player.
// Usage: node preview-host.mjs --studio <engine-root> --project <project-name>
import fs from 'node:fs';
import http from 'node:http';
import path from 'node:path';

const argv = process.argv.slice(2);
const value = (k) => argv[argv.indexOf(k) + 1];
const root = path.resolve(value('--studio') || '.');
const projectName = value('--project') || '';
if (!/^[a-zA-Z0-9_-]+$/.test(projectName)) throw new Error('Invalid project name');
const project = path.resolve(root, 'projects', projectName);
if (!fs.existsSync(path.join(root, 'player.html')) || !fs.existsSync(path.join(project, 'config.json')))
  throw new Error('Studio/player or project config missing');

const mime = {
  '.js': 'text/javascript', '.mjs':'text/javascript', '.json':'application/json',
  '.html':'text/html', '.css':'text/css', '.png':'image/png', '.jpg':'image/jpeg',
  '.jpeg':'image/jpeg', '.woff':'font/woff', '.woff2':'font/woff2', '.svg':'image/svg+xml'
};
function scenes() {
  const all = [], base = path.join(project, 'scenes');
  function walk(dir) {
    if (!fs.existsSync(dir)) return;
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      const f = path.join(dir, e.name);
      if (e.isDirectory()) walk(f);
      else if (e.name.endsWith('.js') && !e.name.startsWith('_'))
        all.push(path.relative(project, f).replaceAll('\\','/'));
    }
  }
  walk(path.join(base,'sets'));
  walk(path.join(base,'shots'));
  if (fs.existsSync(base)) for (const name of fs.readdirSync(base))
    if (name.endsWith('.js') && !name.startsWith('_') && name !== 'cast.js')
      all.push('scenes/' + name);
  return all.sort();
}

const server = http.createServer((req,res) => {
  try {
    const url = new URL(req.url, 'http://localhost');
    if (url.pathname === '/__manifest') {
      res.writeHead(200, {'Content-Type':'application/json','Cache-Control':'no-store'});
      return res.end(JSON.stringify(scenes()));
    }
    if (url.pathname === '/__assets') {
      const d = path.join(project, 'anh');
      const assets = fs.existsSync(d) ? fs.readdirSync(d)
        .filter(f => /\.(png|jpe?g|webp)$/i.test(f))
        .sort().map(f=>({id:path.parse(f).name,file:'anh/'+f})) : [];
      res.writeHead(200, {'Content-Type':'application/json','Cache-Control':'no-store'});
      return res.end(JSON.stringify(assets));
    }
    const underProject = url.pathname.startsWith('/proj/');
    const prefix = underProject ? project : root;
    const local = decodeURIComponent(underProject ? url.pathname.slice(6) : url.pathname.slice(1));
    const full = path.resolve(prefix, local);
    if (full !== prefix && !full.startsWith(prefix + path.sep))
      { res.writeHead(403); return res.end('Forbidden'); }
    if (!fs.existsSync(full) || !fs.statSync(full).isFile())
      { res.writeHead(404); return res.end('Not found'); }
    res.writeHead(200, {'Content-Type':mime[path.extname(full)] || 'application/octet-stream',
      'Cache-Control':'no-store','X-Content-Type-Options':'nosniff'});
    return fs.createReadStream(full).pipe(res);
  } catch(e) {res.writeHead(400);res.end(String(e));}
});
server.listen(0,'127.0.0.1',() => {
  const port = server.address().port;
  console.log('PREVIEW_URL http://127.0.0.1:' + port + '/player.html?proj=/proj&scale=0.5');
});
for (const signal of ['SIGINT','SIGTERM']) process.on(signal,()=>server.close(()=>process.exit(0)));
