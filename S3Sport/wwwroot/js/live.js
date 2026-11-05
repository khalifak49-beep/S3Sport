// S3Sport live layer: SignalR connection, live scores and minute ticking, personal notifications.
// Any element with data-live-match="{id}" is kept up to date; inside it:
//   [data-home-score] [data-away-score] [data-status] [data-minute] [data-pens]
// Initial state comes from the element's own data attributes (data-status, data-period, data-period-start, data-period-duration).
(() => {
    'use strict';
    const S3 = (window.S3 = window.S3 || {});
    const states = new Map();

    /** Running minute text ("23", "45+2", "67") or null when the match is not in play. */
    S3.liveMinute = s => {
        if (s.status !== 'Live' || !s.periodStartedAt) { return null; }
        const duration = s.periodDurationMinutes || 45;
        const base = s.period >= 2 ? duration : 0;
        const elapsed = Math.max(1, Math.floor((Date.now() - Date.parse(s.periodStartedAt)) / 60000) + 1);
        return elapsed > duration ? `${base + duration}+${elapsed - duration}` : `${base + elapsed}`;
    };

    const render = s => {
        document.querySelectorAll(`[data-live-match="${s.matchId}"]`).forEach(el => {
            const set = (sel, v) => el.querySelectorAll(sel).forEach(x => { x.textContent = v; });
            if (s.status === 'Live' || s.status === 'HalfTime' || s.status === 'Finished') {
                set('[data-home-score]', s.homeScore);
                set('[data-away-score]', s.awayScore);
            }
            set('[data-status]', s.statusText);
            const minute = S3.liveMinute(s);
            el.querySelectorAll('[data-minute]').forEach(x => {
                x.textContent = minute ? `${minute}'` : (s.status === 'HalfTime' ? 'استراحة' : '');
                x.classList.toggle('d-none', !minute && s.status !== 'HalfTime');
            });
            el.querySelectorAll('[data-pens]').forEach(x => {
                const has = s.homePenaltyScore != null && s.awayPenaltyScore != null;
                x.textContent = has ? `(ترجيح ${s.homePenaltyScore} - ${s.awayPenaltyScore})` : '';
                x.classList.toggle('d-none', !has);
            });
            if (s.status === 'Finished') {
                el.querySelectorAll('.mc-kickoff').forEach(x => { x.innerHTML = '<span class="text-muted small">انتهت</span>'; });
            }
            el.classList.toggle('is-live', s.status === 'Live' || s.status === 'HalfTime');
            el.dataset.liveStatus = s.status;
        });
    };

    /** Applies a (partial) live state and re-renders every element showing that match. */
    S3.applyLive = s => {
        if (!s || !s.matchId) { return; }
        const merged = Object.assign(states.get(s.matchId) || {}, s);
        states.set(s.matchId, merged);
        render(merged);
    };

    // Seed states from server-rendered attributes.
    document.querySelectorAll('[data-live-match][data-status]').forEach(el => {
        const id = Number(el.dataset.liveMatch);
        if (states.has(id)) { return; }
        states.set(id, {
            matchId: id,
            status: el.dataset.status,
            period: Number(el.dataset.period || 0),
            periodStartedAt: el.dataset.periodStart || null,
            periodDurationMinutes: Number(el.dataset.periodDuration || 45)
        });
    });
    const tick = () => states.forEach(s => {
        if (s.status === 'Live') {
            const m = S3.liveMinute(s);
            document.querySelectorAll(`[data-live-match="${s.matchId}"] [data-minute]`).forEach(x => {
                x.textContent = `${m}'`; x.classList.remove('d-none');
            });
        }
    });
    tick();
    setInterval(tick, 15000);

    // ---------- SignalR ----------
    const matchPage = document.querySelector('[data-match-page]');
    const needsLive = document.querySelector('[data-live-match]');
    const signedIn = document.body.dataset.auth === '1';
    if (!window.signalR || (!needsLive && !matchPage && !signedIn)) { return; }

    const conn = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/live')
        .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();
    S3.live = conn;

    conn.on('scoreUpdate', S3.applyLive);
    conn.on('matchState', s => {
        S3.applyLive(s);
        document.dispatchEvent(new CustomEvent('s3:matchState', { detail: s }));
    });
    conn.on('lineupChanged', e => document.dispatchEvent(new CustomEvent('s3:lineupChanged', { detail: e })));
    conn.on('notification', n => {
        // The open page of that same match already plays the event's sound from the live state.
        const onThatMatch = matchPage && n.url === `/matches/${matchPage.dataset.matchPage}`;
        if (!onThatMatch) { S3.sound?.forNotification(n); }
        const bell = document.querySelector('.bell');
        if (bell) {
            let count = bell.querySelector('.count');
            if (!count) { count = document.createElement('span'); count.className = 'count'; count.textContent = '0'; bell.appendChild(count); }
            count.textContent = String((parseInt(count.textContent, 10) || 0) + 1);
        }
        if (window.Swal) {
            Swal.fire({
                toast: true, position: 'top-start', icon: 'info', title: n.title, text: n.message || '',
                showConfirmButton: false, timer: 7000, timerProgressBar: true
            }).then(r => { if (r.dismiss === Swal.DismissReason.backdrop && n.url) { location.href = n.url; } });
        }
    });

    const join = async () => {
        if (needsLive) { await conn.invoke('JoinLive'); }
        if (matchPage) { await conn.invoke('JoinMatch', Number(matchPage.dataset.matchPage)); }
    };
    conn.onreconnected(join);
    const start = async () => {
        try {
            await conn.start();
            await join();
            document.documentElement.classList.add('live-connected');
        } catch {
            setTimeout(start, 10000);
        }
    };
    start();
})();
