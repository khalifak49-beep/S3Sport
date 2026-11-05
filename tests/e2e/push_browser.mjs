// Browser check (headless Chrome over CDP):
//  1. fan enables device notifications from /Notifications → push.js subscribes and the server stores the device;
//  2. the service worker turns a push message into a system notification (delivered with CDP ServiceWorker.deliverPushMessage);
//  3. the operator types guest-team names in the event dialog and they appear on the public timeline;
//  4. signing out removes this device's subscription.
// Headless Chrome has no connection to Google's push service (pushManager.subscribe never settles), so the browser's
// subscribe() is replaced by a stand-in returning an FCM-style subscription. Encryption and delivery to a push service
// are covered by test_push.sh (push_mock.mjs decrypts every message).
//   node push_browser.mjs <outDir> <fanEmail> <managerEmail> <guestMatchId>   (password Test@1234, BASE env, DB env for sqlcmd)
import { spawn, execFileSync } from 'node:child_process';
import { join } from 'node:path';
import { mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';

const BASE = process.env.BASE || 'http://localhost:5199';
const DB = process.env.DB || 'S3SportDb_Test';
const [OUT, FAN, MGR, MATCH] = process.argv.slice(2);
mkdirSync(OUT, { recursive: true });
const sql = q => execFileSync('sqlcmd', ['-S', '.', '-E', '-d', DB, '-W', '-h', '-1', '-Q', 'SET NOCOUNT ON; ' + q]).toString().trim();
const sleep = ms => new Promise(r => setTimeout(r, ms));
let pass = 0, fail = 0;
const check = (n, c, x = '') => { c ? pass++ : fail++; console.log(`  ${c ? 'PASS' : 'FAIL'}  ${n}${x ? ' — ' + x : ''}`); };

async function browser(port, profile) {
    // Short profile path: Cache Storage files under a long folder exceed Windows' 260-character limit and every cache write fails.
    const dir = join(tmpdir(), `s3p-${profile}-${process.pid}`);
    rmSync(dir, { recursive: true, force: true });
    const proc = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', ['--headless=new', `--remote-debugging-port=${port}`,
        `--user-data-dir=${dir}`, '--no-first-run', 'about:blank'], { stdio: 'ignore' });
    let ws;
    for (let i = 0; i < 50 && !ws; i++) {
        try {
            const t = (await (await fetch(`http://127.0.0.1:${port}/json`)).json()).find(t => t.type === 'page');
            if (t) { ws = new WebSocket(t.webSocketDebuggerUrl); await new Promise(r => ws.addEventListener('open', r)); }
        } catch { } if (!ws) await sleep(200);
    }
    let seq = 0; const pending = new Map();
    ws.addEventListener('message', e => { const m = JSON.parse(e.data); if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.rej(new Error(m.error.message)) : p.res(m.result); } });
    const send = (method, params = {}) => new Promise((res, rej) => { const id = ++seq; pending.set(id, { res, rej }); ws.send(JSON.stringify({ id, method, params })); });
    const evaluate = async e => { const r = await send('Runtime.evaluate', { expression: e, awaitPromise: true, returnByValue: true }); if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description || r.exceptionDetails.text); return r.result.value; };
    const go = async url => { await send('Page.navigate', { url: BASE + url }); for (let i = 0; i < 80; i++) { await sleep(150); try { if (await evaluate('document.readyState') === 'complete') break; } catch { } } await sleep(700); };
    const login = async (email) => {
        await go('/Account/Login');
        await evaluate(`document.querySelector('#Email').value='${email}';document.querySelector('#Password').value='Test@1234';document.querySelector('form[action*="Login"]').submit();`);
        await sleep(2000);
    };
    const shot = async name => { const { data } = await send('Page.captureScreenshot', { format: 'png' }); writeFileSync(join(OUT, name), Buffer.from(data, 'base64')); };
    await send('Page.enable'); await send('Runtime.enable');
    return { proc, ws, send, evaluate, go, login, shot };
}

setTimeout(() => { console.log('TIMEOUT'); process.exit(1); }, 150000).unref();
const fan = await browser(9341, 'fan');
const op = await browser(9342, 'op');
try {
    const fanId = sql(`SELECT Id FROM AspNetUsers WHERE Email='${FAN}'`);
    sql(`DELETE FROM PushSubscriptions WHERE UserId='${fanId}'`);

    console.log('== Enable device notifications');
    await fan.send('Browser.grantPermissions', { origin: BASE, permissions: ['notifications'] });
    await fan.send('Page.addScriptToEvaluateOnNewDocument', { source: `
        (() => {
          const KEY = 's3-fake-push';
          const make = (o) => ({ endpoint: o.endpoint, options: { applicationServerKey: Uint8Array.from(o.key).buffer },
            toJSON: () => ({ endpoint: o.endpoint, keys: { p256dh: 'BFakeDeviceKey', auth: 'FakeAuth' } }),
            unsubscribe: async () => { localStorage.removeItem(KEY); return true; } });
          PushManager.prototype.subscribe = async function (opts) {
            const o = { endpoint: 'https://fcm.googleapis.com/fcm/send/browser-test-' + Date.now(), key: [...new Uint8Array(opts.applicationServerKey)] };
            localStorage.setItem(KEY, JSON.stringify(o)); return make(o);
          };
          PushManager.prototype.getSubscription = async function () { const s = localStorage.getItem(KEY); return s ? make(JSON.parse(s)) : null; };
        })();` });
    await fan.login(FAN);
    await fan.go('/Notifications');
    check('toggle offers enabling', await fan.evaluate(`(async()=>{for(let i=0;i<30;i++){const b=document.querySelector('[data-push-toggle]');if(b&&!b.classList.contains('d-none'))return b.dataset.status;await new Promise(r=>setTimeout(r,200));}return null})()`) === 'off');
    await fan.evaluate(`document.querySelector('[data-push-toggle]').click()`);
    let status = null;
    for (let i = 0; i < 60 && status !== 'on'; i++) { await sleep(500); status = await fan.evaluate(`document.querySelector('[data-push-toggle]').dataset.status`); }
    const stateText = await fan.evaluate(`document.querySelector('[data-push-state]').textContent`);
    check('device subscribed (status on)', status === 'on', stateText);
    const endpoint = sql(`SELECT TOP 1 Endpoint FROM PushSubscriptions WHERE UserId='${fanId}'`);
    check('subscription stored with a real push-service endpoint', /^https:\/\/(fcm\.googleapis\.com|.*\.googleapis\.com)\//.test(endpoint), endpoint.slice(0, 60));
    await fan.shot('notifications-on.png');

    console.log('== Operator: guest-team names');
    await op.login(MGR);
    await op.go(`/MatchControl/Index/${MATCH}`);
    await op.evaluate(`document.querySelector('[data-event="Substitution"][data-side="away"]').click()`);
    await sleep(800);
    const ui = await op.evaluate(`({ selectHidden: document.getElementById('evPlayer').classList.contains('d-none'), textShown: !document.getElementById('evPlayerText').classList.contains('d-none'), relatedShown: !document.getElementById('evRelatedText').classList.contains('d-none') })`);
    check('guest side: name fields instead of player lists', ui.selectHidden && ui.textShown && ui.relatedShown, JSON.stringify(ui));
    const subsBefore = Number(sql(`SELECT COUNT(*) FROM MatchEvents WHERE MatchId=${MATCH} AND EventType=8`));
    await op.evaluate(`document.getElementById('evPlayerText').value='يوسف';document.getElementById('evRelatedText').value='حمد';document.getElementById('eventForm').requestSubmit();`);
    await sleep(2500);
    const last = sql(`SELECT TOP 1 CONCAT(CASE WHEN PlayerNameText=N'يوسف' THEN 1 ELSE 0 END, CASE WHEN RelatedPlayerNameText=N'حمد' THEN 1 ELSE 0 END) FROM MatchEvents WHERE MatchId=${MATCH} AND EventType=8 ORDER BY Id DESC`);
    check('substitution saved with typed names', Number(sql(`SELECT COUNT(*) FROM MatchEvents WHERE MatchId=${MATCH} AND EventType=8`)) === subsBefore + 1 && last === '11');
    await op.evaluate(`document.querySelector('[data-event="Goal"][data-side="home"]').click()`);
    await sleep(800);
    check('own side: player list (not free text)', await op.evaluate(`!document.getElementById('evPlayer').classList.contains('d-none') && document.getElementById('evPlayerText').classList.contains('d-none')`));
    await op.evaluate(`bootstrap.Modal.getInstance('#eventModal').hide()`);
    await op.shot('operator-guest.png');

    await fan.go(`/matches/${MATCH}`);
    check('public timeline shows typed names', await fan.evaluate(`document.getElementById('timeline').textContent.includes('حمد') && document.getElementById('timeline').textContent.includes('يوسف')`));

    console.log('== Service worker shows a push message');
    await fan.go('/Notifications');
    let registrationId = null;
    fan.ws.addEventListener('message', e => {
        const m = JSON.parse(e.data);
        if (m.method === 'ServiceWorker.workerRegistrationUpdated')
            registrationId = m.params.registrations.find(r => !r.isDeleted && r.scopeURL.startsWith(BASE))?.registrationId ?? registrationId;
    });
    await fan.send('ServiceWorker.enable');
    for (let i = 0; i < 20 && !registrationId; i++) await sleep(200);
    // The latest stored notification for this fan, exactly as PushWorker would send it.
    const row = sql(`SELECT TOP 1 CONCAT(Title, '|', ISNULL(Message,''), '|', ISNULL(Url,'/')) FROM Notifications WHERE UserId='${fanId}' ORDER BY Id DESC`);
    const payload = { title: 'اختبار: هدف للفريق', message: 'سعيد (12\')', url: `/matches/${MATCH}` };
    await fan.send('ServiceWorker.deliverPushMessage', { origin: BASE, registrationId, data: JSON.stringify(payload) });
    // Headless Chrome keeps no notification list (getNotifications() is always empty), so record what the worker
    // passes to showNotification by attaching to the service worker target itself.
    const swTarget = (await (await fetch(`http://127.0.0.1:9341/json`)).json()).find(t => t.type === 'service_worker' && t.url.startsWith(BASE));
    const sw = new WebSocket(swTarget.webSocketDebuggerUrl);
    await new Promise(r => sw.addEventListener('open', r));
    const swEval = expr => new Promise(res => {
        const id = Math.floor(Math.random() * 1e9);
        const on = e => { const m = JSON.parse(e.data); if (m.id === id) { sw.removeEventListener('message', on); res(m.result?.result?.value); } };
        sw.addEventListener('message', on);
        sw.send(JSON.stringify({ id, method: 'Runtime.evaluate', params: { expression: expr, returnByValue: true, awaitPromise: true } }));
    });
    await swEval(`self.__shown = []; const orig = self.registration.showNotification.bind(self.registration);
        self.registration.showNotification = (title, options) => { self.__shown.push({ title, body: options.body, url: options.data?.url, dir: options.dir, icon: options.icon }); return orig(title, options); }; 1`);
    await fan.send('ServiceWorker.deliverPushMessage', { origin: BASE, registrationId, data: JSON.stringify(payload) });
    let shown = [];
    for (let i = 0; i < 20 && shown.length === 0; i++) { await sleep(300); shown = await swEval('self.__shown') || []; }
    sw.close();
    check('service worker shows the push as a notification (title, body, link, RTL, icon)',
        shown.some(n => n.title === payload.title && n.body === payload.message && n.url === payload.url && n.dir === 'rtl' && n.icon === '/icons/icon-192.png'),
        JSON.stringify(shown[0] || 'nothing'));
    check('fan has stored notifications to push', row.length > 0);

    console.log('== Sign out removes the device');
    await fan.go('/');
    await fan.evaluate(`document.querySelector('form.js-logout').requestSubmit()`);
    await sleep(3500);
    check('subscription removed on sign-out', sql(`SELECT COUNT(*) FROM PushSubscriptions WHERE UserId='${fanId}'`) === '0');
    check('browser subscription removed', await fan.evaluate(`navigator.serviceWorker.ready.then(r => r.pushManager.getSubscription()).then(s => s === null)`));
} catch (e) { fail++; console.error('ERROR', e.message); }
finally {
    console.log(`\nRESULT: ${pass} passed, ${fail} failed`);
    for (const b of [fan, op]) { b.ws?.close(); b.proc.kill(); }
    setTimeout(() => process.exit(0), 300);
}
