// Headless Chrome via DevTools protocol: log in, visit pages at several widths, screenshot,
// and report any element wider than the viewport (horizontal overflow).
// Usage: node shots.mjs <outDir> [email] [password]
import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const BASE = process.env.BASE || 'http://localhost:5107';
const OUT = process.argv[2];
const EMAIL = process.argv[3] || 'admin@s3sport.om';
const PASSWORD = process.argv[4] || process.env.ADMIN_PW;
mkdirSync(OUT, { recursive: true });

const PORT = 9333;
const chrome = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', [
  '--headless=new', `--remote-debugging-port=${PORT}`, `--user-data-dir=${join(OUT, 'profile')}`,
  '--no-first-run', '--hide-scrollbars', 'about:blank'
], { stdio: 'ignore' });

const sleep = ms => new Promise(r => setTimeout(r, ms));
let ws, seq = 0;
const pending = new Map();
const send = (method, params = {}) => new Promise((resolve, reject) => {
  const id = ++seq;
  pending.set(id, { resolve, reject });
  ws.send(JSON.stringify({ id, method, params }));
});

async function connect() {
  for (let i = 0; i < 50; i++) {
    try {
      const targets = await (await fetch(`http://127.0.0.1:${PORT}/json`)).json();
      const page = targets.find(t => t.type === 'page');
      if (page) {
        ws = new WebSocket(page.webSocketDebuggerUrl);
        await new Promise(r => ws.addEventListener('open', r));
        ws.addEventListener('message', e => {
          const msg = JSON.parse(e.data);
          if (msg.id && pending.has(msg.id)) {
            const p = pending.get(msg.id); pending.delete(msg.id);
            msg.error ? p.reject(new Error(msg.error.message)) : p.resolve(msg.result);
          }
        });
        return;
      }
    } catch { /* not ready */ }
    await sleep(200);
  }
  throw new Error('Chrome did not start');
}

const evaluate = async expr => (await send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true })).result.value;
async function go(url) {
  await send('Page.navigate', { url: BASE + url });
  for (let i = 0; i < 50; i++) { await sleep(150); if (await evaluate('document.readyState') === 'complete') break; }
  await sleep(700); // fonts, cascade fetches
}
async function viewport(width, height) {
  await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: width < 768 });
}
async function shot(name) {
  const { data } = await send('Page.captureScreenshot', { format: 'png' });
  writeFileSync(join(OUT, name + '.png'), Buffer.from(data, 'base64'));
}
// Elements extending past the viewport horizontally (ignores hidden/offcanvas content).
const overflowCheck = `(() => {
  const w = document.documentElement.clientWidth, out = [];
  for (const el of document.querySelectorAll('body *')) {
    const s = getComputedStyle(el); if (s.display === 'none' || s.visibility === 'hidden') continue;
    const r = el.getBoundingClientRect(); if (!r.width) continue;
    if (r.right > w + 1 || r.left < -1) out.push((el.id ? '#' + el.id : el.tagName.toLowerCase() + '.' + [...el.classList].slice(0,2).join('.')) + ' [' + Math.round(r.left) + '..' + Math.round(r.right) + ']');
  }
  return { scrollWidth: document.documentElement.scrollWidth, clientWidth: w, offenders: out.slice(0, 6) };
})()`;

const pages = JSON.parse(process.env.PAGES || '[]');
const widths = JSON.parse(process.env.WIDTHS || '[[390,844],[820,1180],[1440,900]]');

try {
  await connect();
  await send('Page.enable'); await send('Runtime.enable');
  await viewport(1440, 900);
  if (EMAIL !== '-') {
    await go('/Account/Login');
    await evaluate(`document.querySelector('#Email').value=${JSON.stringify(EMAIL)};document.querySelector('#Password').value=${JSON.stringify(PASSWORD)};document.querySelector('form[action*="Login"]').submit();`);
    await sleep(1500);
  }
  for (const [w, h] of widths) {
    await viewport(w, h);
    for (const p of pages) {
      await go(p.url);
      if (p.theme) await evaluate(`S3.setTheme(${JSON.stringify(p.theme)})`);
      if (p.js) { const r = await evaluate(p.js); await sleep(500); if (r !== undefined) console.log(`   ${p.name} js →`, JSON.stringify(r)); }
      const o = await evaluate(overflowCheck);
      const flag = o.scrollWidth > o.clientWidth ? `OVERFLOW ${o.scrollWidth}>${o.clientWidth} ${o.offenders.join(' | ')}` : 'ok';
      console.log(`${w}px ${p.name}: ${flag}`);
      await shot(`${p.name}-${w}`);
    }
  }
} catch (e) { console.error('ERROR', e.message); }
finally { ws?.close(); chrome.kill(); setTimeout(() => process.exit(0), 300); }
