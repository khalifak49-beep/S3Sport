// PWA check on http://localhost (a secure context): service worker install, offline fallback, cached public page,
// private pages never served from cache, manifest installability fields.
import { spawn } from 'node:child_process';
import { join } from 'node:path';
const BASE = process.env.BASE || 'http://localhost:5199';
const OUT = process.argv[2];
const PORT = 9336;
const chrome = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', ['--headless=new', `--remote-debugging-port=${PORT}`, `--user-data-dir=${join(OUT, 'profile')}`, '--no-first-run', 'about:blank'], { stdio: 'ignore' });
const sleep = ms => new Promise(r => setTimeout(r, ms));
let ws, seq = 0; const pending = new Map();
const send = (method, params = {}) => new Promise((res, rej) => { const id = ++seq; pending.set(id, { res, rej }); ws.send(JSON.stringify({ id, method, params })); });
let pass = 0, fail = 0;
const check = (n, c, x = '') => { c ? pass++ : fail++; console.log(`  ${c ? 'PASS' : 'FAIL'}  ${n}${x ? ' — ' + x : ''}`); };
for (let i = 0; i < 50; i++) { try { const t = (await (await fetch(`http://127.0.0.1:${PORT}/json`)).json()).find(t => t.type === 'page'); if (t) { ws = new WebSocket(t.webSocketDebuggerUrl); await new Promise(r => ws.addEventListener('open', r)); break; } } catch { } await sleep(200); }
ws.addEventListener('message', e => { const m = JSON.parse(e.data); if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.rej(new Error(m.error.message)) : p.res(m.result); } });
const evaluate = async e => (await send('Runtime.evaluate', { expression: e, awaitPromise: true, returnByValue: true })).result.value;
const go = async url => { try { await send('Page.navigate', { url: BASE + url }); } catch { } for (let i = 0; i < 60; i++) { await sleep(150); try { if (await evaluate('document.readyState') === 'complete') break; } catch { } } await sleep(800); };
try {
  await send('Page.enable'); await send('Runtime.enable'); await send('Network.enable');
  if (process.argv[3] !== 'offline') {
  await go('/');
  check('service worker activated', await evaluate(`navigator.serviceWorker.ready.then(r => r.active?.state)`) === 'activated');
  await go('/Matches'); // visit a public page while online so it is cached
  await go('/Standings');
  await go('/Favorites'); // private: must not be cached
  const manifest = await evaluate(`fetch('/manifest.webmanifest').then(r => r.json())`);
  check('manifest: standalone, rtl, 192+512 icons', manifest.display === 'standalone' && manifest.dir === 'rtl' && manifest.icons.some(i => i.sizes === '512x512') && manifest.icons.some(i => i.sizes === '192x192'));
  const cached = await evaluate(`caches.keys().then(async ks => { const out = []; for (const k of ks) { const c = await caches.open(k); out.push(...(await c.keys()).map(r => new URL(r.url).pathname)); } return out; })`);
  check('static assets precached', cached.includes('/lib/bootstrap/dist/css/bootstrap.rtl.min.css') && cached.includes('/offline.html'));
  check('public page cached (/Matches)', cached.includes('/Matches'));

  } else {
  // Server is stopped: every request fails, exactly like a phone without coverage.
  await go('/Matches');
  const t0 = await evaluate('document.title');
  check('server down: previously visited public page opens from cache', t0.includes('المباريات'), t0);
  await go('/Players?all=true&never-visited=' + Date.now());
  const t1 = await evaluate('document.title');
  const dbg = await evaluate(`(async () => { const ks = await caches.keys(); const out = {}; for (const k of ks) { const c = await caches.open(k); out[k] = (await c.keys()).map(r => { const u = new URL(r.url); return u.pathname + u.search; }).filter(p => !p.startsWith('/lib') && !p.startsWith('/icons')); } return { url: location.href, controlled: !!navigator.serviceWorker.controller, caches: out }; })()`);
  console.log('   debug:', JSON.stringify(dbg));
  check('server down: unvisited page shows the offline page', t1.includes('غير متصل'), t1);
  await go('/Favorites');
  const t2 = await evaluate('document.title');
  check('server down: private page is never served from cache', t2.includes('غير متصل'), t2);
  }
} catch (e) { fail++; console.error('ERROR', e.message); }
finally { console.log(`\nRESULT: ${pass} passed, ${fail} failed`); ws?.close(); chrome.kill(); setTimeout(() => process.exit(0), 300); }
