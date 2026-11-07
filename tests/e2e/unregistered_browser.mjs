// Browser check (headless Chrome over CDP) of the event dialog for players the platform doesn't know:
//  - a side with no registered players gets a free-text box (shirt number or name), optional;
//  - a side with players gets the list plus "لاعب غير مسجل" which reveals the text box;
//  - a correction of a typed event reopens with the typed text.
//   node unregistered_browser.mjs <outDir> <managerEmail> <matchId>
// The match must be live, its home side must have registered players and its away side none
// (test_team_matches.sh leaves such a match: the last one, against "بلا لاعبين"). Password Test@1234.
import { spawn, execFileSync } from 'node:child_process';
import { join } from 'node:path';
import { mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';

const BASE = process.env.BASE || 'http://localhost:5199';
const DB = process.env.DB || 'S3SportDb_Test';
const [OUT, MGR, MATCH] = process.argv.slice(2);
mkdirSync(OUT, { recursive: true });
const sql = q => execFileSync('sqlcmd', ['-S', '.', '-E', '-d', DB, '-W', '-h', '-1', '-Q', 'SET NOCOUNT ON; ' + q]).toString().trim();
const sleep = ms => new Promise(r => setTimeout(r, ms));
let pass = 0, fail = 0;
const check = (n, c, x = '') => { c ? pass++ : fail++; console.log(`  ${c ? 'PASS' : 'FAIL'}  ${n}${x ? ' — ' + x : ''}`); };

const dir = join(tmpdir(), `s3u-${process.pid}`);
rmSync(dir, { recursive: true, force: true });
const proc = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', ['--headless=new', '--remote-debugging-port=9351',
    `--user-data-dir=${dir}`, '--no-first-run', '--window-size=420,900', 'about:blank'], { stdio: 'ignore' });
setTimeout(() => { console.log('TIMEOUT'); proc.kill(); process.exit(1); }, 120000).unref();
let ws;
for (let i = 0; i < 50 && !ws; i++) {
    try {
        const t = (await (await fetch('http://127.0.0.1:9351/json')).json()).find(t => t.type === 'page');
        if (t) { ws = new WebSocket(t.webSocketDebuggerUrl); await new Promise(r => ws.addEventListener('open', r)); }
    } catch { } if (!ws) await sleep(200);
}
let seq = 0; const pending = new Map();
ws.addEventListener('message', e => { const m = JSON.parse(e.data); if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.rej(new Error(m.error.message)) : p.res(m.result); } });
const send = (method, params = {}) => new Promise((res, rej) => { const id = ++seq; pending.set(id, { res, rej }); ws.send(JSON.stringify({ id, method, params })); });
const evaluate = async e => { const r = await send('Runtime.evaluate', { expression: e, awaitPromise: true, returnByValue: true }); if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description || r.exceptionDetails.text); return r.result.value; };
const go = async url => { await send('Page.navigate', { url: BASE + url }); for (let i = 0; i < 80; i++) { await sleep(150); try { if (await evaluate('document.readyState') === 'complete') break; } catch { } } await sleep(700); };
const shot = async name => { const { data } = await send('Page.captureScreenshot', { format: 'png' }); writeFileSync(join(OUT, name), Buffer.from(data, 'base64')); };
const visible = id => `!document.getElementById('${id}').classList.contains('d-none')`;
const open = async (type, side) => { await evaluate(`document.querySelector('[data-event="${type}"][data-side="${side}"]').click()`); await sleep(800); };
const submit = async () => { await evaluate(`document.getElementById('eventForm').requestSubmit()`); await sleep(2000); };
const count = () => Number(sql(`SELECT COUNT(*) FROM MatchEvents WHERE MatchId=${MATCH}`));
const away = sql(`SELECT AwayTeamId FROM Matches WHERE Id=${MATCH}`), home = sql(`SELECT HomeTeamId FROM Matches WHERE Id=${MATCH}`);

try {
    await send('Page.enable'); await send('Runtime.enable');
    await go('/Account/Login');
    await evaluate(`document.querySelector('#Email').value='${MGR}';document.querySelector('#Password').value='Test@1234';document.querySelector('form[action*="Login"]').submit();`);
    await sleep(2000);
    await go(`/MatchControl/Index/${MATCH}`);

    console.log('== Side without registered players');
    await open('Goal', 'away');
    check('goal: text box instead of a list', await evaluate(`!${visible('evPlayer')} && ${visible('evPlayerText')}`));
    check('placeholder asks for number or name', (await evaluate(`document.getElementById('evPlayerText').placeholder`)).includes('رقم القميص'));
    await shot('unregistered-away-goal.png');
    let n = count();
    await evaluate(`document.getElementById('evPlayerText').value='9'`); await submit();
    check('goal by shirt number saved', count() === n + 1 && sql(`SELECT TOP 1 CASE WHEN PlayerNameText=N'رقم 9' THEN 1 ELSE 0 END FROM MatchEvents WHERE MatchId=${MATCH} ORDER BY Id DESC`) === '1');
    await open('YellowCard', 'away');
    n = count(); await submit();
    check('yellow card with nothing typed saved', count() === n + 1 && sql(`SELECT TOP 1 CONCAT(TeamId,'|',ISNULL(PlayerNameText,'-')) FROM MatchEvents WHERE MatchId=${MATCH} ORDER BY Id DESC`) === `${away}|-`);
    await open('Substitution', 'away');
    check('substitution: two text boxes', await evaluate(`${visible('evPlayerText')} && ${visible('evRelatedText')}`));
    n = count(); await submit();
    check('substitution with nothing typed saved', count() === n + 1);

    console.log('== Side with registered players');
    await open('Goal', 'home');
    check('list shown, text hidden', await evaluate(`${visible('evPlayer')} && !${visible('evPlayerText')}`));
    check('list offers "unregistered player"', await evaluate(`[...document.getElementById('evPlayer').options].some(o => o.value === 'other')`));
    await evaluate(`const s=document.getElementById('evPlayer'); s.value='other'; s.dispatchEvent(new Event('change'))`);
    check('choosing it reveals the text box', await evaluate(visible('evPlayerText')));
    await shot('unregistered-home-other.png');
    n = count();
    await evaluate(`document.getElementById('evPlayerText').value='لاعب زائر'`); await submit();
    check('typed scorer saved without an id', count() === n + 1 && sql(`SELECT TOP 1 CONCAT(TeamId,'|',ISNULL(CAST(PlayerId AS varchar),'-'),'|',CASE WHEN PlayerNameText=N'لاعب زائر' THEN 1 ELSE 0 END) FROM MatchEvents WHERE MatchId=${MATCH} ORDER BY Id DESC`) === `${home}|-|1`);
    await open('Goal', 'home');
    check('text box hidden again on the next dialog', await evaluate(`!${visible('evPlayerText')} && document.getElementById('evPlayer').value === ''`));
    const pid = await evaluate(`[...document.getElementById('evPlayer').options].find(o => o.value && o.value !== 'other').value`);
    await evaluate(`document.getElementById('evPlayer').value='${pid}'`);
    n = count(); await submit();
    check('registered scorer saved by id', count() === n + 1 && sql(`SELECT TOP 1 CONCAT(PlayerId,'|',ISNULL(PlayerNameText,'-')) FROM MatchEvents WHERE MatchId=${MATCH} ORDER BY Id DESC`) === `${pid}|-`);

    console.log('== Correcting a typed event');
    const typedId = sql(`SELECT Id FROM MatchEvents WHERE MatchId=${MATCH} AND PlayerNameText=N'لاعب زائر'`);
    const ok = await evaluate(`(() => { const li = [...document.querySelectorAll('li')].find(li => li.textContent.includes('لاعب زائر'));
        const b = li && [...li.querySelectorAll('button')].find(b => (b.textContent + (b.title || '') + (b.getAttribute('aria-label') || '')).includes('تصحيح'));
        if (!b) return false; b.click(); return true; })()`);
    await sleep(800);
    const c = await evaluate(`({ shown: document.getElementById('correctModal').classList.contains('show'), sel: document.getElementById('cPlayer').value, txt: document.getElementById('cPlayerText').value, vis: ${visible('cPlayerText')} })`);
    check('correction opens with the typed name', ok && c.shown && c.sel === 'other' && c.vis && c.txt === 'لاعب زائر', JSON.stringify(c) + ' event ' + typedId);
} catch (e) {
    check('no script error', false, e.message);
} finally {
    ws.close(); proc.kill();
}
console.log(`\nRESULT: ${pass} passed, ${fail} failed`);
process.exit(fail ? 1 : 0);
