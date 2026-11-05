// S3Sport match sounds, synthesised with Web Audio (no audio files): referee whistles for kick-off / half-time /
// full-time, a fanfare with crowd roar for goals, short cues for cards, missed penalties and substitutions.
// They play while the site is open (match page, or any page when a followed match has news). Push notifications
// that arrive while the site is closed use the phone's own notification sound: browsers (and iOS for web apps)
// do not let a site choose that sound.
// iOS only allows audio after a tap, so the audio context is unlocked on the first touch anywhere.
(() => {
    'use strict';
    const S3 = (window.S3 = window.S3 || {});
    const KEY = 's3-sound';
    const AC = window.AudioContext || window.webkitAudioContext;
    let ctx = null, out = null, noise = null;

    const enabled = () => { try { return localStorage.getItem(KEY) !== 'off'; } catch { return true; } };
    const setEnabled = on => { try { localStorage.setItem(KEY, on ? 'on' : 'off'); } catch { /* storage unavailable */ } syncToggles(); };

    const audio = () => {
        if (!AC) { return null; }
        if (!ctx) {
            ctx = new AC();
            const comp = ctx.createDynamicsCompressor();
            out = ctx.createGain();
            out.gain.value = 0.7;
            out.connect(comp).connect(ctx.destination);
            // One second of white noise, reused for whistle breath and crowd roar.
            noise = ctx.createBuffer(1, ctx.sampleRate, ctx.sampleRate);
            const d = noise.getChannelData(0);
            for (let i = 0; i < d.length; i++) { d[i] = Math.random() * 2 - 1; }
        }
        if (ctx.state === 'suspended') { ctx.resume(); }
        return ctx;
    };
    const unlock = () => {
        const c = audio();
        if (!c) { return; }
        const b = c.createBufferSource(); b.buffer = c.createBuffer(1, 1, c.sampleRate); b.connect(out); b.start();
    };
    ['pointerdown', 'keydown', 'touchend'].forEach(t => document.addEventListener(t, unlock, { once: true, passive: true }));

    const env = (c, t, attack, hold, release, peak) => {
        const g = c.createGain();
        g.gain.setValueAtTime(0.0001, t);
        g.gain.exponentialRampToValueAtTime(peak, t + attack);
        g.gain.setValueAtTime(peak, t + attack + hold);
        g.gain.exponentialRampToValueAtTime(0.0001, t + attack + hold + release);
        return g;
    };
    const noiseSource = (c, t, dur) => {
        const s = c.createBufferSource(); s.buffer = noise; s.loop = true; s.start(t); s.stop(t + dur + 0.1); return s;
    };

    // Pea whistle: ~2.9 kHz tone with the fast "trill" wobble of the pea, a harmonic and a little breath noise.
    const blast = (c, t, dur) => {
        const g = env(c, t, 0.015, Math.max(0.01, dur - 0.08), 0.06, 0.32);
        g.connect(out);
        const tone = c.createOscillator(); tone.type = 'sine'; tone.frequency.value = 2900;
        const trill = c.createOscillator(); trill.frequency.value = 27;
        const depth = c.createGain(); depth.gain.value = 170;
        trill.connect(depth).connect(tone.frequency);
        const harm = c.createOscillator(); harm.type = 'triangle'; harm.frequency.value = 5800;
        const hg = c.createGain(); hg.gain.value = 0.12; depth.connect(harm.frequency);
        tone.connect(g); harm.connect(hg).connect(g);
        const bp = c.createBiquadFilter(); bp.type = 'bandpass'; bp.frequency.value = 3000; bp.Q.value = 6;
        const ng = c.createGain(); ng.gain.value = 0.35;
        noiseSource(c, t, dur).connect(bp).connect(ng).connect(g);
        [tone, trill, harm].forEach(o => { o.start(t); o.stop(t + dur + 0.1); });
    };
    const whistle = (c, t, pattern) => { let at = t; for (const d of pattern) { blast(c, at, d); at += d + 0.16; } };

    // Goal: rising brass fanfare over a swelling crowd roar.
    const goal = (c, t) => {
        const lp = c.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 2600; lp.connect(out);
        [[523.25, 0, 0.13], [659.25, 0.14, 0.13], [783.99, 0.28, 0.13], [1046.5, 0.42, 0.75]].forEach(([f, at, d]) => {
            const g = env(c, t + at, 0.02, d - 0.05, 0.18, 0.22); g.connect(lp);
            [-6, 6].forEach(cents => {
                const o = c.createOscillator(); o.type = 'sawtooth'; o.frequency.value = f; o.detune.value = cents;
                o.connect(g); o.start(t + at); o.stop(t + at + d + 0.25);
            });
        });
        const crowd = c.createBiquadFilter(); crowd.type = 'bandpass'; crowd.frequency.value = 900; crowd.Q.value = 0.7;
        const cg = c.createGain();
        cg.gain.setValueAtTime(0.0001, t);
        cg.gain.exponentialRampToValueAtTime(0.45, t + 0.5);
        cg.gain.setValueAtTime(0.45, t + 1.1);
        cg.gain.exponentialRampToValueAtTime(0.0001, t + 2.4);
        noiseSource(c, t, 2.4).connect(crowd).connect(cg).connect(out);
    };
    const ping = (c, t, f, peak = 0.28) => {
        const g = env(c, t, 0.005, 0.02, 0.3, peak); g.connect(out);
        const o = c.createOscillator(); o.type = 'sine'; o.frequency.value = f; o.connect(g); o.start(t); o.stop(t + 0.4);
    };
    const miss = (c, t) => {
        const g = env(c, t, 0.02, 0.35, 0.25, 0.2); g.connect(out);
        const lp = c.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 1400; lp.connect(g);
        const o = c.createOscillator(); o.type = 'sawtooth';
        o.frequency.setValueAtTime(440, t); o.frequency.exponentialRampToValueAtTime(196, t + 0.55);
        o.connect(lp); o.start(t); o.stop(t + 0.7);
    };
    const sub = (c, t) => {
        [[660, 0], [880, 0.11]].forEach(([f, at]) => {
            const g = env(c, t + at, 0.005, 0.04, 0.08, 0.18); g.connect(out);
            const o = c.createOscillator(); o.type = 'triangle'; o.frequency.value = f; o.connect(g); o.start(t + at); o.stop(t + at + 0.2);
        });
    };

    const SOUNDS = {
        kickoff: (c, t) => whistle(c, t, [0.6]),
        halftime: (c, t) => whistle(c, t, [0.35, 0.6]),
        fulltime: (c, t) => whistle(c, t, [0.3, 0.3, 1.0]),
        goal,
        card: (c, t) => ping(c, t, 1320),
        red: (c, t) => { ping(c, t, 988, 0.3); ping(c, t + 0.16, 740, 0.3); },
        miss,
        sub
    };
    /** Plays a cue now (no-op when sound is off or audio is unavailable). */
    const play = (kind, force = false) => {
        if ((!enabled() && !force) || !SOUNDS[kind]) { return; }
        const c = audio();
        if (!c) { return; }
        SOUNDS[kind](c, c.currentTime + 0.03);
    };

    const BY_EVENT = {
        MatchStarted: 'kickoff', SecondHalfStarted: 'kickoff', FirstHalfEnded: 'halftime', MatchEnded: 'fulltime',
        Goal: 'goal', PenaltyGoal: 'goal', OwnGoal: 'goal', YellowCard: 'card', SecondYellowCard: 'red', RedCard: 'red',
        MissedPenalty: 'miss', Substitution: 'sub'
    };
    const BY_NOTIFICATION = {
        MatchStarted: 'kickoff', SecondHalfStarted: 'kickoff', HalfTime: 'halftime', MatchEnded: 'fulltime',
        Goal: 'goal', Penalty: 'miss', Substitution: 'sub'
    };
    // When several events arrive together, the most important one is heard.
    const PRIORITY = ['goal', 'fulltime', 'red', 'halftime', 'kickoff', 'miss', 'card', 'sub'];

    S3.sound = {
        enabled, setEnabled, play,
        kindOf: type => BY_EVENT[type] || null,
        /** Match-page events (LiveEventDto.type values). */
        forEvents: types => {
            const kinds = types.map(t => BY_EVENT[t]).filter(Boolean);
            const best = PRIORITY.find(k => kinds.includes(k));
            if (best) { play(best); }
        },
        /** Personal notifications pushed over SignalR ({ type, title }). */
        forNotification: n => {
            const kind = n.type === 'Card' ? (/🟥/.test(n.title || '') ? 'red' : 'card') : BY_NOTIFICATION[n.type];
            if (kind) { play(kind); }
        }
    };

    // ---------- UI: [data-sound-toggle] buttons, [data-sound-test="goal"] preview buttons ----------
    function syncToggles() {
        document.querySelectorAll('[data-sound-toggle]').forEach(b => {
            const on = enabled();
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
            b.title = on ? 'إيقاف أصوات المباراة' : 'تشغيل أصوات المباراة';
            const icon = b.querySelector('i');
            if (icon) { icon.className = on ? 'fa-solid fa-volume-high' : 'fa-solid fa-volume-xmark'; }
            const label = b.querySelector('[data-sound-label]');
            if (label) { label.textContent = on ? 'الصوت مفعّل' : 'الصوت متوقف'; }
        });
    }
    document.addEventListener('click', e => {
        const toggle = e.target.closest('[data-sound-toggle]');
        if (toggle) { setEnabled(!enabled()); if (enabled()) { play('kickoff'); } return; }
        const test = e.target.closest('[data-sound-test]');
        if (test) { play(test.dataset.soundTest, true); }
    });
    syncToggles();
})();
