// Public match page: renders the events timeline from the live state and keeps it current
// (SignalR "matchState" pushes; polling fallback when WebSockets are unavailable).
(() => {
    'use strict';
    const page = document.getElementById('matchPage');
    if (!page) { return; }
    const matchId = Number(page.dataset.matchPage);
    let state = JSON.parse(document.getElementById('matchState').textContent);
    const initialStatus = state.status;

    const ICON = {
        Goal: '<i class="fa-solid fa-futbol text-success"></i>',
        PenaltyGoal: '<i class="fa-solid fa-futbol text-success"></i>',
        OwnGoal: '<i class="fa-solid fa-futbol text-danger"></i>',
        MissedPenalty: '<i class="fa-solid fa-xmark text-danger"></i>',
        YellowCard: '<span class="card-icon yellow"></span>',
        RedCard: '<span class="card-icon red"></span>',
        SecondYellowCard: '<span class="card-icon yellow"></span><span class="card-icon red"></span>',
        Substitution: '<i class="fa-solid fa-right-left text-info"></i>'
    };
    const STATE_TEXT = { MatchStarted: 'انطلاق المباراة', FirstHalfEnded: 'نهاية الشوط الأول', SecondHalfStarted: 'بداية الشوط الثاني', MatchEnded: 'نهاية المباراة' };

    // Players of a guest team are typed names without a profile page: plain text.
    const playerLink = (id, name) => {
        if (!id) { return document.createTextNode(name); }
        const a = document.createElement('a');
        a.href = `/players/${id}`;
        a.className = 'text-reset';
        a.textContent = name;
        return a;
    };

    const render = () => {
        const list = document.getElementById('timeline');
        list.replaceChildren();
        const events = state.events.filter(e => !e.isCancelled).slice().reverse();
        if (events.length === 0) {
            const li = document.createElement('li');
            li.className = 'list-group-item bg-transparent text-center text-muted py-4';
            li.textContent = state.status === 'Scheduled' ? 'لم تبدأ المباراة بعد. ستظهر الأحداث هنا مباشرة.' : 'لا توجد أحداث مسجلة.';
            list.append(li);
            return;
        }
        for (const ev of events) {
            const li = document.createElement('li');
            if (STATE_TEXT[ev.type]) {
                li.className = 'list-group-item bg-transparent tl-divider';
                li.textContent = STATE_TEXT[ev.type];
                list.append(li);
                continue;
            }
            li.className = `list-group-item bg-transparent tl-event tl-${ev.side}`;
            const minute = document.createElement('span');
            minute.className = 'tl-minute';
            minute.textContent = `${ev.minute}${ev.extraMinute ? '+' + ev.extraMinute : ''}'`;
            const icon = document.createElement('span');
            icon.className = 'tl-icon';
            icon.innerHTML = ICON[ev.type] || '';
            const body = document.createElement('div');
            body.className = 'tl-body';
            const main = document.createElement('div');
            main.className = 'fw-semibold';
            if (ev.type === 'Substitution') {
                main.append('دخول: ');
                if (ev.relatedPlayerName) { main.append(playerLink(ev.relatedPlayerId, ev.relatedPlayerName)); }
            } else if (ev.playerName) {
                main.append(playerLink(ev.playerId, ev.playerName));
            } else {
                main.textContent = ev.typeText;
            }
            body.append(main);
            const sub = document.createElement('div');
            sub.className = 'small text-muted';
            if (ev.type === 'Substitution' && ev.playerName) { sub.textContent = `خروج: ${ev.playerName}`; }
            else if (ev.relatedPlayerName) { sub.textContent = `صناعة: ${ev.relatedPlayerName}`; }
            else if (ev.type !== 'Goal') { sub.textContent = ev.typeText; }
            if (sub.textContent) { body.append(sub); }
            li.append(minute, icon, body);
            list.append(li);
        }
    };

    // Lineup: team switch, and per-player marks (goals, cards, substitutions) drawn from the same live events.
    // (Delegated: the lineup tab is re-rendered when the operator changes the lineup.)
    const showSide = side => {
        document.querySelectorAll('[data-lineup-show]').forEach(b => {
            const on = b.dataset.lineupShow === side;
            b.classList.toggle('active', on);
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
        document.querySelectorAll('[data-lineup-panel]').forEach(p => p.classList.toggle('d-none', p.dataset.lineupPanel !== side));
    };
    document.addEventListener('click', e => {
        const btn = e.target.closest('[data-lineup-show]');
        if (btn) { showSide(btn.dataset.lineupShow); }
    });

    const minuteText = ev => `${ev.minute}${ev.extraMinute ? '+' + ev.extraMinute : ''}'`;
    const renderLineupMarks = () => {
        const marks = new Map(); // playerId -> { goals, ownGoals, yellow, red, off, on }
        const of = id => { if (!marks.has(id)) { marks.set(id, { goals: 0, ownGoals: 0, yellow: 0, red: 0 }); } return marks.get(id); };
        for (const ev of state.events) {
            if (ev.isCancelled) { continue; }
            if (ev.playerId) {
                const m = of(ev.playerId);
                if (ev.type === 'Goal' || ev.type === 'PenaltyGoal') { m.goals++; }
                else if (ev.type === 'OwnGoal') { m.ownGoals++; }
                else if (ev.type === 'YellowCard') { m.yellow++; }
                else if (ev.type === 'RedCard') { m.red++; }
                else if (ev.type === 'SecondYellowCard') { m.yellow++; m.red++; }
                else if (ev.type === 'Substitution') { m.off = minuteText(ev); }
            }
            if (ev.type === 'Substitution' && ev.relatedPlayerId) { of(ev.relatedPlayerId).on = minuteText(ev); }
        }
        document.querySelectorAll('[data-lineup-player]').forEach(el => {
            const m = marks.get(Number(el.dataset.lineupPlayer)) || { goals: 0, ownGoals: 0, yellow: 0, red: 0 };
            const badges = el.querySelector('[data-badges]');
            const parts = [];
            const ball = (cls, n, title) => `<span class="lp-mark lp-ball ${cls}" title="${title}"><i class="fa-solid fa-futbol"></i>${n > 1 ? `<b>${n}</b>` : ''}</span>`;
            if (m.goals) { parts.push(ball('', m.goals, 'هدف')); }
            if (m.ownGoals) { parts.push(ball('is-own', m.ownGoals, 'هدف عكسي')); }
            if (m.red) { parts.push('<span class="lp-mark lp-card red" title="بطاقة حمراء"></span>'); }
            else if (m.yellow) { parts.push('<span class="lp-mark lp-card yellow" title="بطاقة صفراء"></span>'); }
            badges.innerHTML = parts.join('');
            const sub = el.querySelector('[data-sub]');
            sub.innerHTML = m.off ? `<span class="lp-off" title="خروج"><i class="fa-solid fa-arrow-down"></i>${m.off}</span>`
                : m.on ? `<span class="lp-on" title="دخول"><i class="fa-solid fa-arrow-up"></i>${m.on}</span>` : '';
        });
    };

    // Events already on screen; only events that arrive while the page is open make a sound.
    const heard = new Set(state.events.map(e => e.id));
    const newEventTypes = s => {
        const fresh = s.events.filter(e => !e.isCancelled && !heard.has(e.id));
        s.events.forEach(e => heard.add(e.id));
        return fresh.map(e => e.type);
    };

    const apply = s => {
        if (!s || s.matchId !== matchId) { return; }
        const types = newEventTypes(s);
        S3.sound?.forEvents(types);
        // Kick-off while the page is open: reload once to switch from the fixture view to the live scoreboard
        // (after the whistle has been heard).
        if (initialStatus !== s.status && (initialStatus === 'Scheduled' || initialStatus === 'Postponed')) {
            setTimeout(() => location.reload(), types.length && S3.sound?.enabled() ? 1200 : 0);
            return;
        }
        state = s;
        renderLineupMarks();
        render();
    };
    document.addEventListener('s3:matchState', e => apply(e.detail));

    // Lineup saved or players moved on the pitch: reload just the lineup tab, keeping the team being viewed.
    document.addEventListener('s3:lineupChanged', async e => {
        if (e.detail?.matchId !== matchId) { return; }
        const tab = document.getElementById('tab-lineups');
        if (!tab) { return; }
        try {
            const res = await fetch(location.pathname, { headers: { Accept: 'text/html' } });
            if (!res.ok) { return; }
            const fresh = new DOMParser().parseFromString(await res.text(), 'text/html').getElementById('tab-lineups');
            if (!fresh) { return; }
            const side = tab.querySelector('[data-lineup-show].active')?.dataset.lineupShow;
            tab.innerHTML = fresh.innerHTML;
            if (side && tab.querySelector(`[data-lineup-panel="${side}"]`)) { showSide(side); }
            renderLineupMarks();
        } catch { /* offline: the next visit shows it */ }
    });

    // Fallback polling (e.g. proxies without WebSockets) while the match is in play.
    setInterval(async () => {
        if (document.documentElement.classList.contains('live-connected') || document.hidden) { return; }
        if (!['Live', 'HalfTime'].includes(state.status)) { return; }
        try {
            const res = await fetch(`/matches/${matchId}/state`, { headers: { Accept: 'application/json' } });
            if (res.ok) { const s = await res.json(); S3.applyLive(s); apply(s); }
        } catch { /* offline */ }
    }, 30000);

    render();
    renderLineupMarks();
})();
