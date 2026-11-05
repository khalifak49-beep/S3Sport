// Production build check in a real browser: HTTPS login, CSP violations, JS errors, SignalR, service worker.
import { spawn } from 'node:child_process';
import { join } from 'node:path';
import { mkdirSync, writeFileSync } from 'node:fs';

const BASE = 'https://localhost:5443';
const OWNER_EMAIL = process.env.OWNER_EMAIL || 'owner@s3sport.om';
const OWNER_PW = process.env.OWNER_PW; if (!OWNER_PW) { console.error('set OWNER_PW (and optionally OWNER_EMAIL) to the production test admin account'); process.exit(2); }
const OUT = process.argv[2];
mkdirSync(OUT, { recursive: true });
const PORT = 9335;
const chrome = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', [
  '--headless=new', `--remote-debugging-port=${PORT}`, `--user-data-dir=${join(OUT, 'profile')}`, '--no-first-run',
  '--ignore-certificate-errors', 'about:blank'
], { stdio: 'ignore' });
const sleep = ms => new Promise(r => setTimeout(r, ms));
let ws, seq = 0; const pending = new Map(); const problems = [];
const send = (method, params = {}) => new Promise((res, rej) => { const id = ++seq; pending.set(id, { res, rej }); ws.send(JSON.stringify({ id, method, params })); });
let pass = 0, fail = 0;
const check = (n, c, x = '') => { c ? pass++ : fail++; console.log(`  ${c ? 'PASS' : 'FAIL'}  ${n}${x ? ' — ' + x : ''}`); };

for (let i = 0; i < 50; i++) {
  try {
    const t = (await (await fetch(`http://127.0.0.1:${PORT}/json`)).json()).find(t => t.type === 'page');
    if (t) { ws = new WebSocket(t.webSocketDebuggerUrl); await new Promise(r => ws.addEventListener('open', r)); break; }
  } catch { } await sleep(200);
}
ws.addEventListener('message', e => {
  const m = JSON.parse(e.data);
  if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.rej(new Error(m.error.message)) : p.res(m.result); }
  if (m.method === 'Runtime.exceptionThrown') problems.push('JS: ' + (m.params.exceptionDetails.exception?.description || m.params.exceptionDetails.text).split('\n')[0]);
  if (m.method === 'Log.entryAdded' && m.params.entry.level === 'error' && !/favicon/.test(m.params.entry.text)) problems.push('LOG: ' + m.params.entry.text.slice(0, 160));
});
const evaluate = async e => (await send('Runtime.evaluate', { expression: e, awaitPromise: true, returnByValue: true })).result.value;
const go = async url => { await send('Page.navigate', { url: BASE + url }); for (let i = 0; i < 60; i++) { await sleep(150); if (await evaluate('document.readyState') === 'complete') break; } await sleep(1200); };

try {
  await send('Page.enable'); await send('Runtime.enable'); await send('Log.enable'); await send('Network.enable');
  await send('Security.setIgnoreCertificateErrors', { ignore: true }); // local dev certificate
  // Record CSP violations from the very start of every document.
  await send('Page.addScriptToEvaluateOnNewDocument', { source: `window.__csp=[];document.addEventListener('securitypolicyviolation',e=>window.__csp.push(e.violatedDirective+' '+e.blockedURI));` });
  await send('Emulation.setDeviceMetricsOverride', { width: 1280, height: 900, deviceScaleFactor: 1, mobile: false });

  await go('/Account/Login');
  await evaluate(`document.querySelector('#Email').value=${JSON.stringify(OWNER_EMAIL)};document.querySelector('#Password').value=${JSON.stringify(OWNER_PW)};document.querySelector('form[action*="Login"]').submit();`);
  await sleep(2000);
  const cookies = (await send('Network.getAllCookies').catch(() => ({ cookies: [] }))).cookies || [];
  const auth = cookies.find(c => c.name === 'S3Sport.Auth');
  check('login over HTTPS sets a Secure, HttpOnly auth cookie', !!auth && auth.secure && auth.httpOnly);

  const pages = ['/', '/Matches', '/Teams?all=true', '/Competitions?all=true', '/Standings', '/Players?all=true', '/News', '/Region',
                 '/Favorites', '/Admin/Dashboard', '/Admin/Competitions', '/Admin/News/Create', '/Admin/Matches'];
  for (const p of pages) {
    await go(p);
    const v = await evaluate('window.__csp || []');
    const title = await evaluate('document.title');
    check(`${p} — loaded, no CSP violations`, v.length === 0 && title.includes('S3Sport'), v.length ? v.join(' | ') : title);
  }
  await go('/Matches');
  check('SignalR connects under CSP', await (async () => { for (let i = 0; i < 30; i++) { if (await evaluate(`document.documentElement.classList.contains('live-connected')`)) return true; await sleep(200); } return false; })());
  check('service worker registered', await evaluate(`navigator.serviceWorker.getRegistration().then(r => !!r)`));
  check('Google font stylesheet allowed', await evaluate(`[...document.styleSheets].some(s => (s.href||'').includes('fonts.googleapis'))`));
  await go('/Admin/Dashboard');
  check('dashboard charts render under CSP', await evaluate(`!!document.querySelector('#teamsChart') && document.querySelector('#teamsChart').width > 0`));
  const { data } = await send('Page.captureScreenshot', { format: 'png' }); writeFileSync(join(OUT, 'dashboard.png'), Buffer.from(data, 'base64'));
  check('no JavaScript errors on any page', problems.length === 0, problems.slice(0, 5).join(' | '));
} catch (e) { fail++; console.error('ERROR', e.message); }
finally { console.log(`\nRESULT: ${pass} passed, ${fail} failed`); ws?.close(); chrome.kill(); setTimeout(() => process.exit(0), 300); }
