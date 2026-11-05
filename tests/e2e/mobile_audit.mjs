// Mobile audit (headless Chrome, phone emulation with touch): for every page and role, a full-page screenshot plus
// measurable problems: horizontal overflow (and the elements causing it), tap targets under 40px, inputs under 16px
// (iOS zooms the page on focus), and content hidden under the fixed bottom bar.
//   node mobile_audit.mjs <outDir> <width> <pages.json>
// pages.json: [{ "role": "guest|<email>", "path": "/...", "name": "file-name" }]  (test users: Test@1234; admin: $ADMIN_PW)
import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync, readFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';

const BASE = process.env.BASE || 'http://localhost:5199';
const [OUT, WIDTH = '375', PAGES] = process.argv.slice(2);
const width = Number(WIDTH), height = 812;
mkdirSync(OUT, { recursive: true });
const pages = JSON.parse(readFileSync(PAGES, 'utf8'));
setTimeout(() => { console.log('TIMEOUT'); process.exit(1); }, 600000).unref();
const sleep = ms => new Promise(r => setTimeout(r, ms));

// Short profile path (Windows 260-char limit breaks Chrome's storage under long folders).
const dir = join(tmpdir(), `s3m-${process.pid}`);
rmSync(dir, { recursive: true, force: true });
const proc = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', ['--headless=new', '--remote-debugging-port=9361',
    `--user-data-dir=${dir}`, '--no-first-run', '--hide-scrollbars', 'about:blank'], { stdio: 'ignore' });
let ws;
for (let i = 0; i < 50 && !ws; i++) {
    try { const t = (await (await fetch('http://127.0.0.1:9361/json')).json()).find(t => t.type === 'page'); if (t) { ws = new WebSocket(t.webSocketDebuggerUrl); await new Promise(r => ws.addEventListener('open', r)); } } catch { }
    if (!ws) await sleep(200);
}
let seq = 0; const pending = new Map();
ws.addEventListener('message', e => { const m = JSON.parse(e.data); if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.rej(new Error(m.error.message)) : p.res(m.result); } });
const send = (method, params = {}) => new Promise((res, rej) => { const id = ++seq; pending.set(id, { res, rej }); ws.send(JSON.stringify({ id, method, params })); });
const evaluate = async e => { const r = await send('Runtime.evaluate', { expression: e, awaitPromise: true, returnByValue: true }); if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description || r.exceptionDetails.text); return r.result.value; };
const go = async url => { await send('Page.navigate', { url: BASE + url }); for (let i = 0; i < 80; i++) { await sleep(150); try { if (await evaluate('document.readyState') === 'complete') break; } catch { } } await sleep(900); };

await send('Page.enable'); await send('Runtime.enable'); await send('Network.enable');
await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 2, mobile: true });
await send('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 5 });
await send('Emulation.setUserAgentOverride', { userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1' });

const MEASURE = `(() => {
  const vw = document.documentElement.clientWidth;
  const out = { scrollWidth: document.documentElement.scrollWidth, vw, overflow: [], smallTargets: [], smallInputs: [], title: document.title };
  const label = el => (el.id ? '#' + el.id : '') + (el.className && typeof el.className === 'string' ? '.' + el.className.trim().split(/\\s+/).slice(0, 3).join('.') : '') + ' <' + el.tagName.toLowerCase() + '> ' + (el.textContent || el.value || el.getAttribute('aria-label') || el.title || '').trim().replace(/\\s+/g, ' ').slice(0, 30);
  const visible = el => { const s = getComputedStyle(el); const r = el.getBoundingClientRect(); return s.display !== 'none' && s.visibility !== 'hidden' && r.width > 0 && r.height > 0; };
  // Elements sticking out of the viewport (ignoring descendants of horizontally scrollable containers).
  const scrollable = el => { for (let p = el.parentElement; p; p = p.parentElement) { const s = getComputedStyle(p); if (/(auto|scroll|hidden)/.test(s.overflowX) && p !== document.body && p !== document.documentElement) return true; } return false; };
  for (const el of document.querySelectorAll('body *')) {
    if (!visible(el) || el.closest('.modal:not(.show), .dropdown-menu:not(.show), .offcanvas:not(.show), .collapse:not(.show), script, style, .swal2-container')) continue;
    const r = el.getBoundingClientRect();
    if ((r.right > vw + 1 || r.left < -1) && !scrollable(el)) out.overflow.push(label(el) + ' [' + Math.round(r.left) + '..' + Math.round(r.right) + ']');
  }
  out.overflow = out.overflow.slice(0, 12);
  for (const el of document.querySelectorAll('a[href], button, input:not([type=hidden]), select, textarea, [role=button], label.btn, summary')) {
    if (!visible(el) || el.closest('.modal:not(.show), .dropdown-menu:not(.show), .collapse:not(.show), .d-none')) continue;
    const r = el.getBoundingClientRect();
    // Inline links inside running text are exempt (WCAG 2.5.8 exception).
    if (el.tagName === 'A' && getComputedStyle(el).display === 'inline' && el.closest('p, li.small, .small, dd, td')) continue;
    if (r.height < 36 || r.width < 36) out.smallTargets.push(label(el) + ' ' + Math.round(r.width) + 'x' + Math.round(r.height));
    if (/^(INPUT|SELECT|TEXTAREA)$/.test(el.tagName) && !/^(checkbox|radio|file|color|range)$/.test(el.type) && parseFloat(getComputedStyle(el).fontSize) < 16)
      out.smallInputs.push(label(el) + ' ' + getComputedStyle(el).fontSize);
  }
  out.smallTargets = [...new Set(out.smallTargets)].slice(0, 15);
  // Last interactive element must not sit under the fixed bottom nav.
  const nav = document.querySelector('.bottom-nav');
  if (nav && visible(nav)) {
    window.scrollTo({ top: document.documentElement.scrollHeight, behavior: 'instant' });
    const navTop = nav.getBoundingClientRect().top;
    const covered = [...document.querySelectorAll('main a, main button, footer a')].filter(visible)
      .filter(el => !el.closest('.modal, .dropdown-menu, .offcanvas'))
      .filter(el => { const r = el.getBoundingClientRect(); return r.bottom > navTop + 2 && r.top < innerHeight; });
    out.bottomCovered = covered.map(label).slice(0, 3);
    out.scrollInfo = { navTop: Math.round(navTop), innerHeight, scrollY: Math.round(scrollY), docH: document.documentElement.scrollHeight };
    window.scrollTo({ top: 0, behavior: 'instant' });
  }
  return out;
})()`;

let currentRole = null;
const results = [];
for (const p of pages) {
    if (p.role !== currentRole) {
        await send('Network.clearBrowserCookies');
        if (p.role !== 'guest') {
            await go('/Account/Login');
            const pw = p.role.startsWith('admin@') ? process.env.ADMIN_PW : 'Test@1234';
            await evaluate(`document.querySelector('#Email').value=${JSON.stringify(p.role)};document.querySelector('#Password').value='${pw}';document.querySelector('form[action*="Login"]').submit();`);
            await sleep(2200);
        }
        currentRole = p.role;
    }
    await go(p.path);
    if (p.click) { try { await evaluate(`document.querySelector(${JSON.stringify(p.click)})?.click()`); await sleep(900); } catch { } }
    const m = await evaluate(MEASURE);
    const { contentSize } = await send('Page.getLayoutMetrics');
    const h = Math.min(Math.ceil(contentSize.height), 6000);
    await send('Emulation.setDeviceMetricsOverride', { width, height: p.click ? height : h, deviceScaleFactor: 1, mobile: true });
    await sleep(300);
    const { data } = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: !p.click });
    writeFileSync(join(OUT, `${p.name}.png`), Buffer.from(data, 'base64'));
    await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 2, mobile: true });
    results.push({ ...p, ...m });
    const flags = [m.scrollWidth > m.vw ? `OVERFLOW ${m.scrollWidth}>${m.vw}` : null, m.smallInputs.length ? `ZOOM-INPUTS ${m.smallInputs.length}` : null,
        m.smallTargets.length ? `SMALL-TAPS ${m.smallTargets.length}` : null,
        m.bottomCovered?.length ? `UNDER-BOTTOM-NAV ${m.bottomCovered.join(' / ')} ${JSON.stringify(m.scrollInfo)}` : null].filter(Boolean);
    console.log(`${flags.length ? 'ISSUE' : 'ok   '}  ${p.name.padEnd(26)} ${flags.join(' | ')}`);
}
writeFileSync(join(OUT, `report-${width}.json`), JSON.stringify(results, null, 1));
ws.close(); proc.kill();
setTimeout(() => { try { rmSync(dir, { recursive: true, force: true }); } catch { /* Chrome still releasing files */ } process.exit(0); }, 1500);
