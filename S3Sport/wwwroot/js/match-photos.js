// Match photos: upload by team managers (each photo is shrunk in the browser, then sent one by one),
// and the full-screen viewer (swipe / arrows / keyboard; delete for the uploading team and admins).
(() => {
    'use strict';
    const MAX_SIDE = 1600, QUALITY = 0.85;

    // Open the photos tab when the page is reached with #photos (after an upload, or from a shared link).
    if (location.hash === '#photos') {
        const tab = document.querySelector('[data-bs-target="#tab-photos"]');
        if (tab) { bootstrap.Tab.getOrCreateInstance(tab).show(); tab.scrollIntoView({ inline: 'center', block: 'nearest' }); }
    }

    // ---------- Upload ----------
    const form = document.querySelector('[data-photo-upload]');
    const shrink = async file => {
        try {
            const bmp = await createImageBitmap(file, { imageOrientation: 'from-image' });
            const scale = Math.min(1, MAX_SIDE / Math.max(bmp.width, bmp.height));
            const canvas = document.createElement('canvas');
            canvas.width = Math.round(bmp.width * scale);
            canvas.height = Math.round(bmp.height * scale);
            canvas.getContext('2d').drawImage(bmp, 0, 0, canvas.width, canvas.height);
            bmp.close?.();
            const blob = await new Promise(r => canvas.toBlob(r, 'image/jpeg', QUALITY));
            return blob ? new File([blob], 'photo.jpg', { type: 'image/jpeg' }) : file;
        } catch {
            return file; // the browser cannot decode it: send as is, the server checks it
        }
    };
    if (form) {
        const input = form.querySelector('[data-photo-input]');
        const status = form.querySelector('[data-photo-status]');
        const token = form.querySelector('input[name="__RequestVerificationToken"]').value;
        input.addEventListener('change', async () => {
            const files = [...input.files];
            if (files.length === 0) { return; }
            input.disabled = true;
            let done = 0;
            const errors = [];
            for (const [i, file] of files.entries()) {
                status.textContent = `جارٍ رفع ${i + 1} من ${files.length}…`;
                const body = new FormData();
                body.append('teamId', form.querySelector('[name="teamId"]').value);
                body.append('caption', form.querySelector('[name="caption"]').value);
                body.append('photo', await shrink(file));
                try {
                    const res = await fetch(`/matches/${form.dataset.photoUpload}/photos`, { method: 'POST', body, headers: { RequestVerificationToken: token } });
                    if (res.ok) { done++; continue; }
                    const data = await res.json().catch(() => ({}));
                    errors.push(data.error || 'تعذر رفع صورة.');
                    if (res.status === 400 && /الحد الأقصى/.test(data.error || '')) { break; }
                } catch {
                    errors.push('انقطع الاتصال أثناء الرفع.');
                    break;
                }
            }
            input.disabled = false;
            input.value = ''; // only now: some browsers lose access to the picked files once the input is cleared
            if (done > 0) {
                location.hash = 'photos';
                location.reload();
                return;
            }
            status.textContent = [...new Set(errors)].join(' ');
            status.classList.add('text-danger');
        });
    }

    // ---------- Viewer ----------
    const viewer = document.getElementById('photoViewer');
    const thumbs = [...document.querySelectorAll('.photo-thumb')];
    if (!viewer || thumbs.length === 0) { return; }
    const modal = bootstrap.Modal.getOrCreateInstance(viewer);
    const img = viewer.querySelector('[data-viewer-img]');
    const del = viewer.querySelector('[data-viewer-delete]');
    let index = 0;

    const show = i => {
        index = (i + thumbs.length) % thumbs.length;
        const t = thumbs[index].dataset;
        img.src = t.src;
        img.alt = t.caption || 'صورة من المباراة';
        viewer.querySelector('[data-viewer-caption]').textContent = t.caption || '';
        viewer.querySelector('[data-viewer-team]').textContent = `صورة من ${t.team}`;
        viewer.querySelector('[data-viewer-count]').textContent = `${index + 1} من ${thumbs.length}`;
        del.classList.toggle('d-none', t.canDelete !== '1');
        viewer.querySelectorAll('[data-viewer-step]').forEach(b => b.classList.toggle('d-none', thumbs.length < 2));
    };
    thumbs.forEach((t, i) => t.addEventListener('click', () => { show(i); modal.show(); }));
    viewer.querySelectorAll('[data-viewer-step]').forEach(b => b.addEventListener('click', () => show(index + Number(b.dataset.viewerStep))));
    viewer.addEventListener('keydown', e => {
        // RTL: the left arrow moves forward.
        if (e.key === 'ArrowLeft') { show(index + 1); }
        if (e.key === 'ArrowRight') { show(index - 1); }
    });
    let startX = null;
    img.addEventListener('touchstart', e => { startX = e.touches[0].clientX; }, { passive: true });
    img.addEventListener('touchend', e => {
        if (startX == null) { return; }
        const dx = e.changedTouches[0].clientX - startX;
        startX = null;
        if (Math.abs(dx) > 50) { show(index + (dx > 0 ? 1 : -1)); } // swipe right = next in RTL
    });

    del.addEventListener('click', async () => {
        const ok = window.Swal
            ? (await Swal.fire({ title: 'حذف الصورة؟', icon: 'warning', showCancelButton: true, confirmButtonText: 'حذف', cancelButtonText: 'إلغاء', confirmButtonColor: '#dc2626' })).isConfirmed
            : confirm('حذف الصورة؟');
        if (!ok) { return; }
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const res = await fetch(`/matches/photos/${thumbs[index].dataset.photoId}/delete`, { method: 'POST', headers: { RequestVerificationToken: token } });
        if (res.ok) { location.hash = 'photos'; location.reload(); }
        else { const d = await res.json().catch(() => ({})); window.Swal ? Swal.fire({ icon: 'error', text: d.error || 'تعذر الحذف.' }) : alert(d.error || 'تعذر الحذف.'); }
    });
})();
