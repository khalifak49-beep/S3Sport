// S3Sport Web Push: device notifications (goals, cards, kick-off...) even when the site is closed.
// Loaded for signed-in users only. Needs a secure context (HTTPS or localhost) and the service worker.
// iPhone/iPad: available only after "Add to Home Screen" (iOS 16.4+).
(() => {
    'use strict';

    const S3 = (window.S3 = window.S3 || {});
    const supported = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window && window.isSecureContext;

    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const post = (url, data) => fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded', RequestVerificationToken: token() },
        body: new URLSearchParams({ ...data, __RequestVerificationToken: token() })
    });

    // base64url VAPID key → Uint8Array, as pushManager.subscribe expects.
    const keyBytes = b64 => {
        const raw = atob((b64 + '='.repeat((4 - b64.length % 4) % 4)).replace(/-/g, '+').replace(/_/g, '/'));
        return Uint8Array.from(raw, c => c.charCodeAt(0));
    };
    const sameKey = (buffer, b64) => {
        if (!buffer) { return false; }
        const a = new Uint8Array(buffer), b = keyBytes(b64);
        return a.length === b.length && a.every((v, i) => v === b[i]);
    };

    let serverKey = null;
    const getServerKey = async () => {
        if (!serverKey) {
            const r = await fetch('/Push/Key', { headers: { Accept: 'application/json' } });
            if (!r.ok) { throw new Error('key'); }
            serverKey = (await r.json()).publicKey;
        }
        return serverKey;
    };

    const send = sub => {
        const json = sub.toJSON();
        return post('/Push/Subscribe', { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth });
    };

    /** 'unsupported' | 'denied' | 'on' | 'off' */
    S3.pushStatus = async () => {
        if (!supported) { return 'unsupported'; }
        if (Notification.permission === 'denied') { return 'denied'; }
        const reg = await navigator.serviceWorker.getRegistration();
        const sub = reg && await reg.pushManager.getSubscription();
        return sub && Notification.permission === 'granted' ? 'on' : 'off';
    };

    /** Asks for permission (must follow a user click) and registers this device. Returns the new status. */
    S3.enablePush = async () => {
        if (!supported) { return 'unsupported'; }
        const permission = await Notification.requestPermission();
        if (permission !== 'granted') { return permission === 'denied' ? 'denied' : 'off'; }
        const reg = await navigator.serviceWorker.register('/sw.js').then(() => navigator.serviceWorker.ready);
        const key = await getServerKey();
        let sub = await reg.pushManager.getSubscription();
        if (sub && !sameKey(sub.options.applicationServerKey, key)) { await sub.unsubscribe(); sub = null; }
        sub = sub || await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(key) });
        const r = await send(sub);
        if (!r.ok) { throw new Error('subscribe'); }
        try { sessionStorage.setItem('s3-push-synced', '1'); } catch { /* storage unavailable */ }
        return 'on';
    };

    /** Stops notifications on this device only. */
    S3.disablePush = async () => {
        const reg = supported && await navigator.serviceWorker.getRegistration();
        const sub = reg && await reg.pushManager.getSubscription();
        if (sub) {
            await post('/Push/Unsubscribe', { endpoint: sub.endpoint }).catch(() => { });
            await sub.unsubscribe();
        }
        return 'off';
    };

    // Once per session: re-send an existing subscription so the server has it for the signed-in account,
    // and renew it if the server's key changed (the old one would be rejected by the push service).
    (async () => {
        try {
            if (!supported || Notification.permission !== 'granted' || sessionStorage.getItem('s3-push-synced')) { return; }
            const reg = await navigator.serviceWorker.getRegistration();
            const sub = reg && await reg.pushManager.getSubscription();
            if (!sub) { return; }
            const key = await getServerKey();
            if (!sameKey(sub.options.applicationServerKey, key)) { await S3.enablePush(); return; }
            if ((await send(sub)).ok) { sessionStorage.setItem('s3-push-synced', '1'); }
        } catch { /* best effort */ }
    })();

    // Signing out: this device must stop receiving the account's notifications.
    document.addEventListener('submit', async e => {
        const form = e.target;
        if (!(form instanceof HTMLFormElement) || !form.classList.contains('js-logout') || form.dataset.pushDone) { return; }
        e.preventDefault();
        try { await Promise.race([S3.disablePush(), new Promise(r => setTimeout(r, 2500))]); } catch { /* sign out anyway */ }
        try { sessionStorage.removeItem('s3-push-synced'); } catch { /* storage unavailable */ }
        form.dataset.pushDone = '1';
        form.submit();
    });

    // ---------- UI: [data-push-toggle] button with [data-push-state] text (notifications page) ----------
    const TEXT = {
        on: ['إشعارات هذا الجهاز مفعّلة: تصلك الأهداف والأحداث حتى لو كان الموقع مغلقاً.', 'إيقاف على هذا الجهاز', 'btn-soft'],
        off: ['فعّل الإشعارات ليصلك كل هدف وحدث لما تتابعه، حتى لو كان الموقع مغلقاً.', 'تفعيل على هذا الجهاز', 'btn-primary'],
        denied: ['الإشعارات محظورة لهذا الموقع في إعدادات المتصفح. اسمح بها من إعدادات الموقع ثم أعد المحاولة.', null, null],
        unsupported: ['هذا المتصفح لا يدعم الإشعارات. على آيفون: أضف الموقع إلى الشاشة الرئيسية ثم افتحه منها.', null, null]
    };
    const render = (box, status) => {
        const [text, label, cls] = TEXT[status];
        box.querySelector('[data-push-state]').textContent = text;
        const btn = box.querySelector('[data-push-toggle]');
        btn.classList.toggle('d-none', !label);
        if (label) {
            btn.textContent = label;
            btn.classList.remove('btn-soft', 'btn-primary');
            btn.classList.add(cls);
            btn.dataset.status = status;
        }
    };
    document.querySelectorAll('[data-push-box]').forEach(async box => {
        render(box, await S3.pushStatus());
        box.querySelector('[data-push-toggle]').addEventListener('click', async e => {
            const btn = e.currentTarget;
            btn.disabled = true;
            try {
                render(box, btn.dataset.status === 'on' ? await S3.disablePush() : await S3.enablePush());
            } catch {
                S3.toast?.('error', 'تعذر تفعيل الإشعارات. تحقق من الاتصال وأعد المحاولة.');
            } finally {
                btn.disabled = false;
            }
        });
    });

    // After following something, offer device notifications once (only if the browser has not been asked yet).
    S3.offerPush = async () => {
        if (!supported || Notification.permission !== 'default' || !window.Swal) { return; }
        try { if (localStorage.getItem('s3-push-offered')) { return; } localStorage.setItem('s3-push-offered', '1'); } catch { /* storage unavailable */ }
        const r = await Swal.fire({
            title: 'إشعارات على جهازك؟',
            text: 'يصلك كل هدف وبطاقة وتبديل وبداية ونهاية لما تتابعه، حتى لو كان الموقع مغلقاً.',
            icon: 'question', showCancelButton: true, confirmButtonText: 'تفعيل', cancelButtonText: 'لاحقاً', reverseButtons: true
        });
        if (r.isConfirmed) {
            try {
                const status = await S3.enablePush();
                if (status === 'on') { S3.toast?.('success', 'تم تفعيل الإشعارات على هذا الجهاز.'); }
            } catch { S3.toast?.('error', 'تعذر تفعيل الإشعارات.'); }
        }
    };
})();
