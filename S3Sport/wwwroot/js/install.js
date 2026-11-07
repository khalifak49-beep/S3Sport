// "Install the app": a bar on phones (first visits), the home-page card, and the how-to window (Views/Shared/_InstallApp.cshtml).
// Android/Chrome: the browser's own install dialog (beforeinstallprompt) when it is offered, otherwise the menu steps.
// iPhone: Safari has no install API, so the window shows the Share → "Add to Home Screen" steps.
(() => {
    'use strict';
    const standalone = window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
    if (standalone) { return; } // already the installed app: show nothing

    const ua = navigator.userAgent;
    const isIOS = /iphone|ipad|ipod/i.test(ua) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    const isAndroid = /android/i.test(ua);
    const isPhone = isIOS || isAndroid;
    const inApp = /FBAN|FBAV|Instagram|Snapchat|musical_ly|TikTok|Line\//i.test(ua);
    const DISMISS_KEY = 's3_install_dismissed', SNOOZE_DAYS = 7;

    const bar = document.querySelector('[data-install-bar]');
    const modalEl = document.getElementById('installModal');
    if (!modalEl) { return; }
    let deferred = null; // the browser's install prompt, when offered

    const store = {
        get: () => { try { return Number(localStorage.getItem(DISMISS_KEY) || 0); } catch { return 0; } },
        set: v => { try { localStorage.setItem(DISMISS_KEY, String(v)); } catch { /* storage blocked */ } }
    };
    const snoozed = () => Date.now() - store.get() < SNOOZE_DAYS * 86400000;

    const showTab = os => {
        modalEl.querySelectorAll('[data-install-tab]').forEach(b => {
            const on = b.dataset.installTab === os;
            b.classList.toggle('active', on);
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
        modalEl.querySelectorAll('[data-install-panel]').forEach(p => p.classList.toggle('d-none', p.dataset.installPanel !== os));
    };
    modalEl.querySelectorAll('[data-install-tab]').forEach(b => b.addEventListener('click', () => showTab(b.dataset.installTab)));
    modalEl.querySelector('[data-install-inapp]')?.classList.toggle('d-none', !inApp);

    const hideAll = () => {
        bar?.classList.add('d-none');
        document.querySelectorAll('[data-install-card]').forEach(c => c.classList.add('d-none'));
    };

    const nativeInstall = async () => {
        if (!deferred) { return false; }
        const prompt = deferred;
        try { await prompt.prompt(); } catch { return false; } // refused (e.g. not from a tap): keep it, show the steps instead
        deferred = null;
        const { outcome } = await prompt.userChoice;
        if (outcome === 'accepted') { hideAll(); }
        return true;
    };

    const open = async () => {
        if (await nativeInstall()) { return; }
        showTab(isIOS ? 'ios' : 'android');
        bootstrap.Modal.getOrCreateInstance(modalEl).show();
    };
    document.addEventListener('click', e => {
        if (e.target.closest('[data-install-open]')) { e.preventDefault(); open(); }
        if (e.target.closest('[data-install-dismiss]')) { store.set(Date.now()); bar?.classList.add('d-none'); }
    });
    modalEl.querySelector('[data-install-native]')?.addEventListener('click', async () => {
        bootstrap.Modal.getInstance(modalEl)?.hide();
        await nativeInstall();
    });

    // The home-page card is shown to everyone who is not in the installed app.
    document.querySelectorAll('[data-install-card]').forEach(c => c.classList.remove('d-none'));

    const showBar = () => { if (bar && isPhone && !snoozed()) { bar.classList.remove('d-none'); } };

    window.addEventListener('beforeinstallprompt', e => {
        e.preventDefault(); // our own bar replaces Chrome's mini banner
        deferred = e;
        modalEl.querySelector('[data-install-native]')?.classList.remove('d-none');
        showBar();
    });
    window.addEventListener('appinstalled', () => { deferred = null; hideAll(); });

    // Phones that never get the browser prompt (iPhone, Samsung Internet, in-app browsers) still see the bar.
    setTimeout(showBar, 2500);
})();
