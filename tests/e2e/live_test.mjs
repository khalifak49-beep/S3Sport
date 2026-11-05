// Real-browser SignalR test: an anonymous fan watches /matches/{id} while an operator (separate browser
// context, logged in) uses the operator UI. Verifies kick-off, a goal via the UI modal, live score/timeline
// on the fan page without reload, and a duplicate-safe resend. Usage: node live_test.mjs <matchId> <outDir>
import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const BASE = process.env.BASE || 'http://localhost:5199';
const ADMIN_PW = process.env.ADMIN_PW; if (!ADMIN_PW) { console.error("set ADMIN_PW to the admin password of the test instance (see README.md)"); process.exit(2); }
const MATCH = process.argv[2];
const OUT = process.argv[3];
mkdirSync(OUT, { recursive: true });
const PORT = 9334;
const chrome = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', [
  '--headless=new', `--remote-debugging-port=${PORT}`, `--user-data-dir=${join(OUT, 'profile')}`, '--no-first-run', 'about:blank'
], { stdio: 'ignore' });
const sleep = ms => new Promise(r => setTimeout(r, ms));
let ws, seq = 0;
const pending = new Map();
const send = (method, params = {}, sessionId) => new Promise((resolve, reject) => {
  const id = ++seq; pending.set(id, { resolve, reject });
  ws.send(JSON.stringify({ id, method, params, ...(sessionId ? { sessionId } : {}) }));
});
let pass = 0, fail = 0;
const check = (name, cond, extra = '') => { cond ? pass++ : fail++; console.log(`  ${cond ? 'PASS' : 'FAIL'}  ${name}${extra ? ' — ' + extra : ''}`); };

async function connect() {
  for (let i = 0; i < 50; i++) {
    try {
      const v = await (await fetch(`http://127.0.0.1:${PORT}/json/version`)).json();
      ws = new WebSocket(v.webSocketDebuggerUrl);
      await new Promise(r => ws.addEventListener('open', r));
      ws.addEventListener('message', e => {
        const m = JSON.parse(e.data);
        if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result); }
      });
      return;
    } catch { await sleep(200); }
  }
  throw new Error('no chrome');
}
async function openTab(contextId) {
  const { targetId } = await send('Target.createTarget', { url: 'about:blank', ...(contextId ? { browserContextId: contextId } : {}) });
  const { sessionId } = await send('Target.attachToTarget', { targetId, flatten: true });
  await send('Page.enable', {}, sessionId); await send('Runtime.enable', {}, sessionId);
  await send('Emulation.setDeviceMetricsOverride', { width: 430, height: 900, deviceScaleFactor: 1, mobile: true }, sessionId);
  return sessionId;
}
const evaluate = async (s, expr) => (await send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true }, s)).result.value;
async function go(s, url) {
  await send('Page.navigate', { url: BASE + url }, s);
  for (let i = 0; i < 60; i++) { await sleep(150); if (await evaluate(s, 'document.readyState') === 'complete') break; }
  await sleep(900);
}
async function waitFor(s, expr, ms = 8000) {
  for (let t = 0; t < ms; t += 200) { if (await evaluate(s, expr)) return true; await sleep(200); }
  return false;
}
async function shot(s, name) { const { data } = await send('Page.captureScreenshot', { format: 'png' }, s); writeFileSync(join(OUT, name + '.png'), Buffer.from(data, 'base64')); }

try {
  await connect();
  const fan = await openTab();
  const { browserContextId } = await send('Target.createBrowserContext');
  const op = await openTab(browserContextId);

  // Operator logs in (separate cookie jar).
  await go(op, '/Account/Login');
  await evaluate(op, `document.querySelector('#Email').value='admin@s3sport.om';document.querySelector('#Password').value=${JSON.stringify(ADMIN_PW)};document.querySelector('form[action*="Login"]').submit();`);
  await sleep(1500);

  await go(fan, `/matches/${MATCH}`);
  check('fan: SignalR connected', await waitFor(fan, `document.documentElement.classList.contains('live-connected')`));
  await go(op, `/MatchControl/Index/${MATCH}`);
  check('operator: SignalR connected', await waitFor(op, `document.documentElement.classList.contains('live-connected')`));
  await shot(op, 'operator-before');

  // Kick-off through the UI: state button → SweetAlert confirm.
  await evaluate(op, `document.querySelector('[data-state-event="MatchStarted"]').click()`);
  await waitFor(op, `!!document.querySelector('.swal2-confirm')`);
  await evaluate(op, `document.querySelector('.swal2-confirm').click()`);
  check('operator: status Live after kick-off', await waitFor(op, `document.querySelector('[data-status]').textContent.includes('مباشر')`));
  // Fan page reloads once on kick-off (fixture → live scoreboard), then stays live without reloads.
  check('fan: switched to live scoreboard', await waitFor(fan, `document.querySelector('.scoreboard [data-status]')?.textContent.includes('مباشر')`, 10000));
  await sleep(1500);
  await evaluate(fan, `window.__noReload = 1`);

  // Goal through the UI: home "هدف" pad → modal → choose first real player → submit.
  await evaluate(op, `document.querySelector('[data-event="Goal"][data-side="home"]').click()`);
  await waitFor(op, `document.getElementById('eventModal').classList.contains('show')`);
  const title = await evaluate(op, `document.getElementById('eventModalTitle').textContent`);
  const homeName = await evaluate(op, `document.querySelector('[data-side-name="home"]').textContent`);
  check('operator: dialog title names the team', title.includes(homeName) && homeName.length > 2, title);
  const scorer = await evaluate(op, `(() => { const s = document.getElementById('evPlayer'); s.selectedIndex = 1; return s.options[1]?.text || ''; })()`);
  const requestId = await evaluate(op, `document.querySelector('#eventForm [name="ClientRequestId"]').value`);
  await evaluate(op, `document.getElementById('eventSubmit').click()`);
  check('operator: modal closed after save', await waitFor(op, `!document.getElementById('eventModal').classList.contains('show')`));
  check('operator: score 1-0', await waitFor(op, `document.querySelector('[data-home-score]').textContent.trim()==='1'`));

  check('fan: score updated live to 1', await waitFor(fan, `document.querySelector('.scoreboard [data-home-score]').textContent.trim()==='1'`));
  const scorerName = scorer.replace(/^\d+ · /, '').replace(/ \(.*\)$/, '');
  check('fan: goal in timeline', await waitFor(fan, `document.getElementById('timeline').textContent.includes(${JSON.stringify(scorerName)})`), scorerName);
  check('fan: page did not reload', await evaluate(fan, `window.__noReload === 1`));
  check('fan: live minute shown', await waitFor(fan, `/\\d+'/.test(document.querySelector('.scoreboard [data-minute]').textContent)`));

  // Resend the very same click (e.g. network retry) from the operator page: must be ignored.
  const resend = await evaluate(op, `(async () => {
      const body = new FormData(); body.append('EventType','Goal'); body.append('ClientRequestId', ${JSON.stringify(requestId)});
      body.append('TeamId', document.getElementById('op').dataset.homeTeam);
      const token = document.querySelector('#op input[name="__RequestVerificationToken"]').value;
      const r = await fetch('/MatchControl/AddEvent/${MATCH}', { method: 'POST', body, headers: { RequestVerificationToken: token } });
      return await r.json(); })()`);
  check('duplicate resend flagged and ignored', resend?.duplicate === true && resend?.state?.homeScore === 1, JSON.stringify({ duplicate: resend?.duplicate, score: resend?.state?.homeScore }));

  await sleep(800);
  await shot(fan, 'fan-live');
  await shot(op, 'operator-after');
} catch (e) { fail++; console.error('ERROR', e.message); }
finally {
  console.log(`\nRESULT: ${pass} passed, ${fail} failed`);
  ws?.close(); chrome.kill(); setTimeout(() => process.exit(0), 300);
}
