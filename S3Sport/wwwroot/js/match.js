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
        render();
    };
    document.addEventListener('s3:matchState', e => apply(e.detail));

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
})();
