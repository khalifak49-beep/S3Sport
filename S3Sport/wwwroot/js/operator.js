// Match operator screen: state transitions, event entry, corrections. Talks to MatchControlController (JSON).
// Every click gets a ClientRequestId (UUID) that is reused on retries, so a double tap or a resend
// after a network error can never record the same event twice.
(() => {
    'use strict';
    const root = document.getElementById('op');
    if (!root) { return; }

    const matchId = Number(root.dataset.matchId);
    const token = root.querySelector('input[name="__RequestVerificationToken"]').value;
    const teams = { home: Number(root.dataset.homeTeam), away: Number(root.dataset.awayTeam) };
    const isKnockout = root.dataset.knockout === '1';
    const squads = {
        home: JSON.parse(document.getElementById('opHomeSquad').textContent),
        away: JSON.parse(document.getElementById('opAwaySquad').textContent)
    };
    // A team without registered players (a guest team, or an opponent that added none) gets a free-text box for
    // the shirt number or name, optional. A team with players gets the list plus "unregistered player" → text box.
    const guest = { home: root.dataset.homeGuest === '1', away: root.dataset.awayGuest === '1' };
    const OTHER = 'other';
    const textOnly = side => guest[side] || squads[side].length === 0;
    const usePlayerInputs = (selectId, textId, side) => {
        const select = document.getElementById(selectId), text = document.getElementById(textId);
        select.dataset.side = side;
        const only = textOnly(side), typed = only || select.value === OTHER;
        select.classList.toggle('d-none', only); select.disabled = only;
        text.classList.toggle('d-none', !typed); text.disabled = !typed;
        if (!typed) { text.value = ''; }
        return typed ? text : select;
    };
    const setPlayer = (selectId, textId, side, id, name) => {
        const select = document.getElementById(selectId), text = document.getElementById(textId);
        select.value = id ?? (name && !textOnly(side) ? OTHER : '');
        usePlayerInputs(selectId, textId, side);
        if (id == null && name) { text.value = name; }
    };
    const dropOther = data => {
        if (data.PlayerId === OTHER) { delete data.PlayerId; }
        if (data.RelatedPlayerId === OTHER) { delete data.RelatedPlayerId; }
        return data;
    };
    for (const [s, t] of [['evPlayer', 'evPlayerText'], ['evRelated', 'evRelatedText'], ['cPlayer', 'cPlayerText'], ['cRelated', 'cRelatedText']]) {
        document.getElementById(s).addEventListener('change', e => {
            usePlayerInputs(s, t, e.target.dataset.side);
            if (e.target.value === OTHER) { document.getElementById(t).focus(); }
        });
    }
    let state = JSON.parse(document.getElementById('opState').textContent);
    const eventModal = new bootstrap.Modal('#eventModal');
    const correctModal = new bootstrap.Modal('#correctModal');
    // Bootstrap ignores hide() while the opening animation runs; a fast save would leave the dialog open.
    const closeModal = (modal, el) => {
        modal.hide();
        setTimeout(() => { if (el.classList.contains('show')) { modal.hide(); } }, 450);
    };
    const uuid = () => (crypto.randomUUID ? crypto.randomUUID()
        : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => { const r = Math.random() * 16 | 0; return (c === 'x' ? r : (r & 3 | 8)).toString(16); }));

    const TITLES = {
        Goal: 'هدف', PenaltyGoal: 'هدف من ركلة جزاء', OwnGoal: 'هدف عكسي', MissedPenalty: 'ركلة جزاء ضائعة',
        YellowCard: 'بطاقة صفراء', SecondYellowCard: 'إنذار ثانٍ (طرد)', RedCard: 'بطاقة حمراء', Substitution: 'تبديل',
        MatchStarted: 'بداية المباراة', FirstHalfEnded: 'نهاية الشوط الأول', SecondHalfStarted: 'بداية الشوط الثاني', MatchEnded: 'نهاية المباراة'
    };
    const ICONS = {
        Goal: 'fa-solid fa-futbol text-success', PenaltyGoal: 'fa-solid fa-bullseye text-success', OwnGoal: 'fa-solid fa-futbol text-danger',
        MissedPenalty: 'fa-solid fa-xmark text-danger', Substitution: 'fa-solid fa-right-left text-info',
        MatchStarted: 'fa-solid fa-play', FirstHalfEnded: 'fa-solid fa-pause', SecondHalfStarted: 'fa-solid fa-play', MatchEnded: 'fa-solid fa-flag-checkered'
    };
    const STATE_ACTIONS = {
        Scheduled: ['MatchStarted', 'بدء المباراة', 'fa-play', 'btn-primary'],
        Postponed: ['MatchStarted', 'بدء المباراة', 'fa-play', 'btn-primary'],
        LiveP1: ['FirstHalfEnded', 'نهاية الشوط الأول', 'fa-pause', 'btn-warning'],
        HalfTime: ['SecondHalfStarted', 'بداية الشوط الثاني', 'fa-play', 'btn-primary'],
        LiveP2: ['MatchEnded', 'نهاية المباراة', 'fa-flag-checkered', 'btn-danger']
    };
    const isStateType = t => ['MatchStarted', 'FirstHalfEnded', 'SecondHalfStarted', 'MatchEnded'].includes(t);
    const sideOf = teamId => (teamId === teams.home ? 'home' : 'away');

    // ---------- networking ----------
    const post = async (url, data) => {
        const body = new FormData();
        Object.entries(data).forEach(([k, v]) => { if (v !== null && v !== undefined && v !== '') { body.append(k, v); } });
        const res = await fetch(url, { method: 'POST', body, headers: { RequestVerificationToken: token, 'X-Requested-With': 'fetch' } });
        let json = null;
        try { json = await res.json(); } catch { /* non-JSON (e.g. session expired) */ }
        if (!res.ok || !json || !json.ok) { throw new Error((json && json.error) || 'تعذر الحفظ. تحقق من الاتصال ثم أعد المحاولة.'); }
        state = json.state;
        render();
        return json;
    };
    const refresh = async () => {
        const res = await fetch(`/MatchControl/State/${matchId}`, { headers: { Accept: 'application/json' } });
        if (res.ok) { state = await res.json(); render(); }
    };
    document.addEventListener('s3:matchState', e => { if (e.detail.matchId === matchId) { refresh(); } });

    // ---------- rendering ----------
    const currentMinute = () => {
        const m = S3.liveMinute(state);
        if (!m) { return { minute: state.period >= 2 ? 46 : 1, extra: null }; }
        const [base, extra] = m.split('+').map(Number);
        return { minute: base, extra: extra || null };
    };

    const render = () => {
        S3.applyLive(state);
        root.querySelectorAll('[data-live-dot]').forEach(d => d.classList.toggle('d-none', state.status !== 'Live'));

        const key = state.status === 'Live' ? (state.period >= 2 ? 'LiveP2' : 'LiveP1') : state.status;
        const action = STATE_ACTIONS[key];
        const box = document.getElementById('stateAction');
        box.replaceChildren();
        if (action) {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = `btn btn-lg ${action[3]} px-4`;
            btn.dataset.stateEvent = action[0];
            btn.innerHTML = `<i class="fa-solid ${action[2]} me-2"></i>`;
            btn.append(action[1]);
            box.append(btn);
        } else if (state.status === 'Finished') {
            box.innerHTML = '<span class="badge text-bg-success fs-6">انتهت المباراة — يمكنك تصحيح الأحداث أو التراجع عن النهاية</span>';
        } else if (state.status === 'Cancelled') {
            box.innerHTML = '<span class="badge text-bg-secondary fs-6">المباراة ملغاة</span>';
        }

        document.querySelectorAll('#eventPads [data-event]').forEach(b => {
            b.disabled = !(state.status === 'Live' || (state.status === 'HalfTime' && b.dataset.event === 'Substitution'));
        });
        renderTimeline();
    };

    const renderTimeline = () => {
        const list = document.getElementById('timeline');
        list.replaceChildren();
        const events = [...state.events].reverse();
        if (events.length === 0) {
            const li = document.createElement('li');
            li.className = 'list-group-item bg-transparent text-muted text-center py-4';
            li.textContent = 'لا توجد أحداث بعد. ابدأ المباراة من الزر أعلاه.';
            list.append(li);
            return;
        }
        for (const ev of events) {
            const li = document.createElement('li');
            li.className = `list-group-item bg-transparent op-event side-${ev.side || 'none'}${ev.isCancelled ? ' is-cancelled' : ''}`;

            const minute = document.createElement('span');
            minute.className = 'op-minute';
            minute.textContent = isStateType(ev.type) && ev.minute <= 1 ? '' : `${ev.minute}${ev.extraMinute ? '+' + ev.extraMinute : ''}'`;

            const icon = document.createElement('span');
            icon.className = 'op-icon';
            if (ev.type === 'YellowCard') { icon.innerHTML = '<span class="card-icon yellow"></span>'; }
            else if (ev.type === 'RedCard') { icon.innerHTML = '<span class="card-icon red"></span>'; }
            else if (ev.type === 'SecondYellowCard') { icon.innerHTML = '<span class="card-icon yellow"></span><span class="card-icon red"></span>'; }
            else { icon.innerHTML = `<i class="${ICONS[ev.type] || 'fa-solid fa-circle'}"></i>`; }

            const text = document.createElement('div');
            text.className = 'flex-grow-1 min-w-0';
            const title = document.createElement('div');
            title.className = 'fw-semibold';
            title.textContent = ev.typeText + (ev.side ? ` — ${ev.side === 'home' ? 'المستضيف' : 'الضيف'}` : '');
            text.append(title);
            const detail = [];
            if (ev.type === 'Substitution') {
                if (ev.relatedPlayerName) { detail.push(`دخول: ${ev.relatedPlayerName}`); }
                if (ev.playerName) { detail.push(`خروج: ${ev.playerName}`); }
            } else {
                if (ev.playerName) { detail.push(ev.playerName); }
                if (ev.relatedPlayerName) { detail.push(`صناعة: ${ev.relatedPlayerName}`); }
            }
            if (ev.notes) { detail.push(ev.notes); }
            if (ev.isCancelled) { detail.push('ملغى'); }
            if (ev.revisionCount > 0) { detail.push(`عُدّل ${ev.revisionCount}×`); }
            if (detail.length) {
                const d = document.createElement('div');
                d.className = 'small text-muted';
                d.textContent = detail.join(' · ');
                text.append(d);
            }

            const actions = document.createElement('div');
            actions.className = 'd-flex gap-1';
            const btn = (label, cls, handler) => {
                const b = document.createElement('button');
                b.type = 'button'; b.className = `btn btn-soft btn-sm ${cls}`; b.textContent = label;
                b.addEventListener('click', handler);
                actions.append(b);
            };
            if (!ev.isCancelled) { btn('تصحيح', '', () => openCorrection(ev)); }
            else if (!isStateType(ev.type)) { btn('استعادة', 'text-success', () => cancelEvent(ev, true)); }

            li.append(minute, icon, text, actions);
            list.append(li);
        }
    };

    // ---------- player selects ----------
    const fillPlayers = (select, side, placeholder, preferLineup) => {
        select.replaceChildren();
        select.add(new Option(placeholder, ''));
        const players = [...squads[side]].sort((a, b) => (preferLineup ? (b.inLineup - a.inLineup) : 0) || ((a.number ?? 999) - (b.number ?? 999)));
        for (const p of players) {
            const label = `${p.number != null ? p.number + ' · ' : ''}${p.name}${p.isStarter ? ' (أساسي)' : p.inLineup ? ' (احتياط)' : ''}`;
            select.add(new Option(label, p.id));
        }
        if (!textOnly(side)) { select.add(new Option('✎ لاعب غير مسجل — اكتب الرقم أو الاسم', OTHER)); }
    };

    // ---------- new events ----------
    const eventForm = document.getElementById('eventForm');
    const show = (id, visible) => document.getElementById(id).classList.toggle('d-none', !visible);

    const openEvent = (type, side) => {
        eventForm.reset();
        document.getElementById('eventError').textContent = '';
        eventForm.EventType.value = type;
        eventForm.TeamId.value = side ? teams[side] : '';
        eventForm.ClientRequestId.value = uuid();
        const teamName = side ? root.querySelector(`[data-side-name="${side}"]`).textContent : '';
        document.getElementById('eventModalTitle').textContent = TITLES[type] + (teamName ? ` — ${teamName}` : '');

        const isState = isStateType(type);
        show('playerField', !isState);
        show('relatedField', ['Goal', 'Substitution'].includes(type));
        show('penaltyFields', type === 'MatchEnded');
        if (!isState) {
            document.getElementById('evPlayerLabel').textContent =
                type === 'Substitution' ? 'اللاعب الخارج' : type === 'OwnGoal' ? 'اللاعب (من هذا الفريق)' : 'اللاعب';
            document.getElementById('evRelatedLabel').textContent = type === 'Substitution' ? 'اللاعب الداخل' : 'صانع الهدف (اختياري)';
            fillPlayers(document.getElementById('evPlayer'), side, '— غير محدد —', true);
            fillPlayers(document.getElementById('evRelated'), side, type === 'Substitution' ? '— اختر —' : '— بدون —', type !== 'Substitution');
            usePlayerInputs('evPlayer', 'evPlayerText', side);
            usePlayerInputs('evRelated', 'evRelatedText', side);
        }
        const now = currentMinute();
        eventForm.Minute.value = isState && type === 'MatchStarted' ? 0 : now.minute;
        eventForm.ExtraMinute.value = now.extra ?? '';
        eventModal.show();
        if (!isState) { setTimeout(() => document.getElementById(textOnly(side) ? 'evPlayerText' : 'evPlayer').focus(), 300); }
    };

    eventForm.addEventListener('submit', async e => {
        e.preventDefault();
        const submit = document.getElementById('eventSubmit');
        if (submit.disabled) { return; }
        submit.disabled = true;
        const data = dropOther(Object.fromEntries(new FormData(eventForm).entries()));
        try {
            const json = await post(`/MatchControl/AddEvent/${matchId}`, data);
            closeModal(eventModal, document.getElementById('eventModal'));
            S3.toast('success', json.duplicate ? 'سُجل هذا الحدث مسبقاً.' : 'تم التسجيل.');
        } catch (err) {
            document.getElementById('eventError').textContent = err.message; // same ClientRequestId is kept for the retry
        } finally {
            submit.disabled = false;
        }
    });

    document.getElementById('eventPads').addEventListener('click', e => {
        const b = e.target.closest('[data-event]');
        if (b && !b.disabled) { openEvent(b.dataset.event, b.dataset.side); }
    });

    // State transitions: confirm, then send (a drawn knockout final whistle asks for penalties).
    document.getElementById('stateAction').addEventListener('click', async e => {
        const b = e.target.closest('[data-state-event]');
        if (!b) { return; }
        const type = b.dataset.stateEvent;
        if (type === 'MatchEnded' && isKnockout && state.homeScore === state.awayScore) {
            openEvent(type, null);
            return;
        }
        const r = await Swal.fire({
            title: `${TITLES[type]}؟`, icon: 'question', showCancelButton: true, reverseButtons: true,
            confirmButtonText: 'تأكيد', cancelButtonText: 'إلغاء', focusCancel: true
        });
        if (!r.isConfirmed) { return; }
        b.disabled = true;
        const requestId = uuid();
        const now = currentMinute();
        try {
            await post(`/MatchControl/AddEvent/${matchId}`, {
                EventType: type, ClientRequestId: requestId,
                Minute: type === 'MatchStarted' ? 0 : now.minute, ExtraMinute: now.extra
            });
            S3.toast('success', TITLES[type]);
        } catch (err) {
            Swal.fire({ icon: 'error', title: 'تنبيه', text: err.message, confirmButtonText: 'حسناً' });
        } finally {
            b.disabled = false;
        }
    });

    // ---------- corrections ----------
    const correctForm = document.getElementById('correctForm');
    let editing = null;

    const openCorrection = ev => {
        editing = ev;
        correctForm.reset();
        document.getElementById('correctError').textContent = '';
        correctForm.eventId.value = ev.id;
        const isState = isStateType(ev.type);
        show('cTeamField', !isState);
        show('cPlayerField', !isState);
        show('cRelatedField', !isState && ['Goal', 'Substitution'].includes(ev.type));
        document.getElementById('correctModalTitle').textContent = `تصحيح: ${ev.typeText}`;
        if (!isState) {
            const side = ev.side || 'home';
            correctForm.TeamId.value = teams[side];
            fillCorrectionPlayers(side);
            setPlayer('cPlayer', 'cPlayerText', side, ev.playerId, ev.playerName);
            setPlayer('cRelated', 'cRelatedText', side, ev.relatedPlayerId, ev.relatedPlayerName);
        }
        correctForm.Minute.value = ev.minute;
        correctForm.ExtraMinute.value = ev.extraMinute ?? '';
        correctForm.Notes.value = ev.notes ?? '';
        correctModal.show();
    };
    const fillCorrectionPlayers = side => {
        fillPlayers(document.getElementById('cPlayer'), side, '— غير محدد —', false);
        fillPlayers(document.getElementById('cRelated'), side, '— بدون —', false);
        usePlayerInputs('cPlayer', 'cPlayerText', side);
        usePlayerInputs('cRelated', 'cRelatedText', side);
    };
    document.getElementById('cTeam').addEventListener('change', e => fillCorrectionPlayers(sideOf(Number(e.target.value))));

    correctForm.addEventListener('submit', async e => {
        e.preventDefault();
        const data = dropOther(Object.fromEntries(new FormData(correctForm).entries()));
        if (!data.Reason || !data.Reason.trim()) { document.getElementById('correctError').textContent = 'سبب التصحيح مطلوب.'; return; }
        if (isStateType(editing.type)) { delete data.TeamId; delete data.PlayerId; delete data.RelatedPlayerId; delete data.PlayerName; delete data.RelatedPlayerName; }
        try {
            await post(`/MatchControl/CorrectEvent/${matchId}?eventId=${data.eventId}`, data);
            closeModal(correctModal, document.getElementById('correctModal'));
            S3.toast('success', 'تم حفظ التصحيح.');
        } catch (err) {
            document.getElementById('correctError').textContent = err.message;
        }
    });

    const cancelEvent = async (ev, restore) => {
        const r = await Swal.fire({
            title: restore ? 'استعادة الحدث؟' : `إلغاء «${ev.typeText}»؟`,
            text: restore ? '' : 'يُحذف أثره من النتيجة والإحصائيات ويبقى في السجل.',
            input: 'text', inputPlaceholder: 'السبب (مطلوب)', icon: restore ? 'question' : 'warning',
            showCancelButton: true, reverseButtons: true, confirmButtonText: 'تأكيد', cancelButtonText: 'رجوع',
            inputValidator: v => (!v || !v.trim() ? 'السبب مطلوب' : undefined)
        });
        if (!r.isConfirmed) { return false; }
        try {
            await post(`/MatchControl/CancelEvent/${matchId}?eventId=${ev.id}&restore=${restore}`, { reason: r.value });
            S3.toast('success', restore ? 'تمت الاستعادة.' : 'تم إلغاء الحدث.');
            return true;
        } catch (err) {
            Swal.fire({ icon: 'error', title: 'تنبيه', text: err.message, confirmButtonText: 'حسناً' });
            return false;
        }
    };
    document.getElementById('cCancelEvent').addEventListener('click', async () => {
        correctModal.hide();
        if (editing) { await cancelEvent(editing, false); }
    });

    // ---------- lineup toggles: a player is either starter or substitute ----------
    root.querySelectorAll('.lineup-toggle input').forEach(input => {
        const sync = () => input.closest('.lineup-toggle').classList.toggle('active', input.checked);
        sync();
        input.addEventListener('change', () => {
            if (input.checked) {
                input.closest('.lineup-row').querySelectorAll('.lineup-toggle input').forEach(o => {
                    if (o !== input) { o.checked = false; o.closest('.lineup-toggle').classList.remove('active'); }
                });
            }
            sync();
        });
    });

    // ---------- pitch arrangement: line per starter (select) and order within the line (arrows) ----------
    const arrange = document.getElementById('pitchArrange');
    if (arrange) {
        const refresh = team => {
            team.querySelectorAll('[data-line]').forEach(line => {
                const slots = [...line.querySelectorAll('.pitch-slot')];
                line.classList.toggle('is-empty', slots.length === 0);
                slots.forEach((s, i) => {
                    s.querySelector('[data-order]').value = i + 1;
                    s.querySelector('[data-move="-1"]').disabled = i === 0;
                    s.querySelector('[data-move="1"]').disabled = i === slots.length - 1;
                });
            });
            // Formation (e.g. 4-3-3) only when exactly one goalkeeper, like the public pitch.
            const count = code => team.querySelectorAll(`[data-code="${code}"] .pitch-slot`).length;
            const outfield = ['DF', 'MF', 'FW'].map(count).filter(n => n > 0);
            team.querySelector('[data-formation]').textContent = count('GK') === 1 && outfield.length ? outfield.join('-') : '';
        };
        arrange.querySelectorAll('[data-pitch-team]').forEach(refresh);
        arrange.addEventListener('click', e => {
            const btn = e.target.closest('[data-move]');
            if (!btn) { return; }
            const slot = btn.closest('.pitch-slot');
            if (btn.dataset.move === '-1') { slot.previousElementSibling?.classList.contains('pitch-slot') && slot.parentElement.insertBefore(slot, slot.previousElementSibling); }
            else if (slot.nextElementSibling) { slot.parentElement.insertBefore(slot.nextElementSibling, slot); }
            refresh(slot.closest('[data-pitch-team]'));
            btn.disabled ? slot.querySelector('[data-move]:not(:disabled)')?.focus() : btn.focus();
        });
        arrange.addEventListener('change', e => {
            const select = e.target.closest('[data-line-select]');
            if (!select) { return; }
            const team = select.closest('[data-pitch-team]');
            team.querySelector(`[data-line="${select.value}"]`).appendChild(select.closest('.pitch-slot'));
            refresh(team);
            select.focus();
        });
    }

    render();
})();
