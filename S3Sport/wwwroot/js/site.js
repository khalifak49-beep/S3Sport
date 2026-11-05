// S3Sport — shared front-end behaviour (theme, alerts, confirmations, cascading region selects, image previews).
(() => {
    'use strict';

    const S3 = (window.S3 = window.S3 || {});
    const root = document.documentElement;

    // ---------- Theme (dark default, remembered per browser) ----------
    const syncThemeIcons = () => {
        const dark = root.getAttribute('data-bs-theme') === 'dark';
        document.querySelectorAll('[data-theme-icon]').forEach(i => {
            i.className = dark ? 'fa-solid fa-sun' : 'fa-solid fa-moon';
        });
    };
    S3.setTheme = theme => {
        root.setAttribute('data-bs-theme', theme);
        try { localStorage.setItem('s3s-theme', theme); } catch { /* storage unavailable */ }
        syncThemeIcons();
        document.dispatchEvent(new CustomEvent('s3:theme', { detail: theme }));
    };
    document.addEventListener('click', e => {
        if (e.target.closest('[data-theme-toggle]')) {
            S3.setTheme(root.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark');
        }
    });

    // ---------- Alerts ----------
    S3.toast = (icon, title) => {
        if (!window.Swal) { return; }
        Swal.fire({
            toast: true, position: 'top-start', icon, title,
            showConfirmButton: false, timer: 4000, timerProgressBar: true
        });
    };

    // <form data-confirm="عنوان" data-confirm-text="تفاصيل"> → SweetAlert confirmation before submitting.
    document.addEventListener('submit', e => {
        const form = e.target;
        if (!(form instanceof HTMLFormElement) || !form.dataset.confirm || form.dataset.confirmed === '1') { return; }
        e.preventDefault();
        const submitter = e.submitter;
        Swal.fire({
            title: form.dataset.confirm,
            text: form.dataset.confirmText || '',
            icon: form.dataset.confirmIcon || 'warning',
            showCancelButton: true,
            confirmButtonText: form.dataset.confirmButton || 'نعم، متابعة',
            cancelButtonText: 'إلغاء',
            confirmButtonColor: form.dataset.confirmDanger !== undefined ? '#e11d48' : getComputedStyle(root).getPropertyValue('--s3-brand').trim(),
            reverseButtons: true,
            focusCancel: true
        }).then(result => {
            if (!result.isConfirmed) { return; }
            form.dataset.confirmed = '1';
            form.requestSubmit ? form.requestSubmit(submitter) : form.submit();
        });
    });

    // ---------- Follow / notifications (no page reload) ----------
    // The server re-renders the _FollowButton partial and we swap it in place.
    // Any failure falls back to a normal form post, which works without JavaScript too.
    document.addEventListener('submit', async e => {
        const form = e.target;
        if (!(form instanceof HTMLFormElement) || !form.classList.contains('js-follow')) { return; }
        e.preventDefault();
        const box = form.closest('[data-follow-box]');
        box?.classList.add('is-busy');
        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                headers: { 'X-Requested-With': 'fetch' }
            });
            if (!response.ok || response.redirected) { throw new Error(String(response.status)); }
            const html = await response.text();
            const message = response.headers.get('X-S3-Message');
            box.outerHTML = html;
            if (message) { S3.toast('success', decodeURIComponent(message)); }
            if (/\/Follows\/Follow$/i.test(new URL(form.action).pathname)) { setTimeout(() => S3.offerPush?.(), 1200); }
        } catch {
            box?.classList.remove('is-busy');
            form.submit();
        }
    });

    // ---------- Cascading selects ----------
    // <select data-cascade-parent="#GovernorateId" data-cascade-url="/Region/Wilayats?governorateId=" data-selected="4">
    // Children reload whenever their parent changes; data-selected is applied once (initial server value).
    const cascadeSelects = [...document.querySelectorAll('select[data-cascade-parent]')];

    const loadOptions = async select => {
        const parent = document.querySelector(select.dataset.cascadeParent);
        const placeholder = select.dataset.placeholder;
        const pending = (select.dataset.selected || '').split(',').filter(Boolean);
        delete select.dataset.selected;

        select.innerHTML = '';
        if (placeholder && !select.multiple) { select.add(new Option(placeholder, '')); }

        const parentValue = parent ? parent.value : '';
        if (!parentValue || parentValue === '0') {
            select.disabled = true;
            select.dispatchEvent(new Event('change'));
            return;
        }

        select.disabled = true;
        try {
            const response = await fetch(select.dataset.cascadeUrl + encodeURIComponent(parentValue), { headers: { Accept: 'application/json' } });
            const items = response.ok ? await response.json() : [];
            const exclude = select.dataset.cascadeExclude ? document.querySelector(select.dataset.cascadeExclude)?.value : null;
            items.filter(i => String(i.id) !== exclude)
                .forEach(i => select.add(new Option(i.name, i.id, false, pending.includes(String(i.id)))));
            select.disabled = items.length === 0;
            if (items.length === 0 && select.dataset.emptyText) {
                select.innerHTML = '';
                select.add(new Option(select.dataset.emptyText, ''));
            }
        } catch {
            S3.toast('error', 'تعذر تحميل القائمة. تحقق من الاتصال.');
        }
        select.dispatchEvent(new Event('change'));
    };

    // Attach every listener first, then start the root-level loads: deeper selects are initialised
    // by their parent's change event, so they must already be listening when it fires.
    const roots = [];
    cascadeSelects.forEach(select => {
        const parent = document.querySelector(select.dataset.cascadeParent);
        if (!parent) { return; }
        parent.addEventListener('change', () => loadOptions(select));
        if (!parent.matches('select[data-cascade-parent]')) { roots.push(select); }
    });
    roots.forEach(loadOptions);

    // ---------- Image preview ----------
    // <input type="file" data-preview="#logoPreview">
    document.addEventListener('change', e => {
        const input = e.target;
        if (!(input instanceof HTMLInputElement) || input.type !== 'file' || !input.dataset.preview) { return; }
        const target = document.querySelector(input.dataset.preview);
        const file = input.files && input.files[0];
        if (!target || !file) { return; }
        if (file.size > 2 * 1024 * 1024) {
            S3.toast('error', 'حجم الصورة يجب ألا يتجاوز 2 ميجابايت.');
            input.value = '';
            return;
        }
        target.src = URL.createObjectURL(file);
        target.classList.remove('d-none');
    });

    // ---------- Auto-submit filters ----------
    // <select data-autosubmit> submits its form on change.
    document.addEventListener('change', e => {
        if (e.target.matches && e.target.matches('[data-autosubmit]')) { e.target.form?.submit(); }
    });

    document.addEventListener('DOMContentLoaded', syncThemeIcons);
    syncThemeIcons();
})();

// ---------- PWA: register the service worker (installable app + offline fallback) ----------
if ('serviceWorker' in navigator && window.isSecureContext) {
    window.addEventListener('load', () => {
        navigator.serviceWorker.register('/sw.js').catch(() => { /* unsupported / blocked: the site works without it */ });
    });
}

// Scrollable chip strips (match days): bring the selected chip into view on load.
document.querySelectorAll('.day-strip .day-chip.active').forEach(chip => {
    chip.scrollIntoView({ inline: 'center', block: 'nearest' });
});

// ---------- Phone tables: <table class="table-stack"> becomes one card per row under 576px (see site.css) ----------
// Each cell gets its column name as a label; the main cell (data-stack-main, 1-based, default 1) is the card title,
// columns with an empty header (actions) form the card's button row, data-stack-hide="1,3" hides columns on phones.
const stackTable = table => {
    const heads = [...table.querySelectorAll('thead th')].map(th => th.textContent.trim() || th.getAttribute('title') || '');
    const main = Number(table.dataset.stackMain || 1) - 1;
    const hide = (table.dataset.stackHide || '').split(',').filter(Boolean).map(n => Number(n) - 1);
    table.querySelectorAll('tbody tr').forEach(tr => {
        if (tr.children.length !== heads.length) { return; } // empty-state rows (colspan)
        [...tr.children].forEach((td, i) => {
            if (hide.includes(i)) { td.classList.add('stack-hide'); }
            else if (i === main) { td.classList.add('stack-main'); }
            else if (!heads[i] || td.querySelector('.table-actions')) { td.classList.add('stack-actions'); }
            else if (!td.hasAttribute('data-label')) { td.dataset.label = heads[i]; }
        });
    });
};
document.querySelectorAll('table.table-stack').forEach(stackTable);

// The status bar follows the site theme (light/dark toggle), not only the phone's setting.
const syncThemeColor = () => {
    const dark = document.documentElement.getAttribute('data-bs-theme') !== 'light';
    document.querySelectorAll('meta[name="theme-color"]').forEach(m => { m.content = dark ? '#0a0f1c' : '#ffffff'; m.removeAttribute('media'); });
};
syncThemeColor();
document.addEventListener('s3:theme', syncThemeColor);
