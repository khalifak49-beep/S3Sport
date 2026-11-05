// S3Sport service worker.
// - Static assets (libraries, css, js, icons): cache-first, refreshed when CACHE_VERSION changes.
// - Pages: network-first (scores must be fresh), falling back to the cached copy, then to /offline.html.
// - Never cached: SignalR (/hubs), admin and operator screens, POSTs, JSON state endpoints, uploads.
// - Web Push: shows the notifications PushWorker sends (see js/push.js for the subscription).
const CACHE_VERSION = 's3sport-v2';
const STATIC_CACHE = `${CACHE_VERSION}-static`;
const PAGE_CACHE = `${CACHE_VERSION}-pages`;
const PRECACHE = [
    '/offline.html',
    '/css/site.css',
    '/js/site.js',
    '/js/live.js',
    '/js/sounds.js',
    '/lib/bootstrap/dist/css/bootstrap.rtl.min.css',
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
    '/lib/fontawesome/css/all.min.css',
    '/lib/sweetalert2/sweetalert2.all.min.js',
    '/lib/signalr/signalr.min.js',
    '/lib/jquery/dist/jquery.min.js',
    '/icons/icon-192.png',
    '/icons/icon-512.png'
];

self.addEventListener('install', event => {
    event.waitUntil(caches.open(STATIC_CACHE).then(c => c.addAll(PRECACHE)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(k => !k.startsWith(CACHE_VERSION)).map(k => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

// Only public pages are stored for offline reading (never favorites, notifications, account pages...).
const isPublicPage = url => /^\/($|matches|teams|competitions|standings|players|news)(\/|$|\?)/i.test(url.pathname + (url.search ? '?' : ''));
const isStatic = url => /^\/(lib|css|js|icons|webfonts)\//.test(url.pathname) || url.pathname.startsWith('/lib/fontawesome/webfonts/');
const neverCache = url =>
    url.pathname.startsWith('/hubs/') || url.pathname.startsWith('/Admin') || url.pathname.startsWith('/MatchControl') ||
    url.pathname.startsWith('/Account') || url.pathname.endsWith('/state') || url.pathname.startsWith('/uploads/');

self.addEventListener('fetch', event => {
    const req = event.request;
    const url = new URL(req.url);
    if (req.method !== 'GET' || url.origin !== self.location.origin || neverCache(url)) { return; }

    if (isStatic(url)) {
        event.respondWith(
            caches.match(req).then(hit => hit || fetch(req).then(res => {
                if (res.ok) { const copy = res.clone(); caches.open(STATIC_CACHE).then(c => c.put(req, copy)); }
                return res;
            }))
        );
        return;
    }

    if (req.mode === 'navigate') {
        event.respondWith(
            fetch(req).then(res => {
                if (res.ok && isPublicPage(url) && !res.headers.get('Cache-Control')?.includes('no-store')) {
                    const copy = res.clone(); caches.open(PAGE_CACHE).then(c => c.put(req, copy));
                }
                return res;
            }).catch(() => caches.match(req).then(hit => hit || caches.match('/offline.html')))
        );
    }
});

// Web Push (VAPID) — payload: { title, message, url }.
// A notification is always shown (iOS revokes subscriptions whose pushes show nothing). When the site is open and
// visible, it is shown silently: the page plays its own match sound (whistle, goal…) instead of the system sound.
self.addEventListener('push', event => {
    const data = event.data ? event.data.json() : {};
    event.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(windows => {
        const visible = windows.some(w => w.visibilityState === 'visible');
        return self.registration.showNotification(data.title || 'S3Sport', {
            body: data.message || '', icon: '/icons/icon-192.png', badge: '/icons/icon-192.png', dir: 'rtl', lang: 'ar',
            silent: visible, data: { url: data.url || '/' }
        });
    }));
});

// Tapping a notification reuses an open window of the app when there is one.
self.addEventListener('notificationclick', event => {
    event.notification.close();
    const url = new URL(event.notification.data?.url || '/', self.location.origin).href;
    event.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(windows => {
        const open = windows.find(w => new URL(w.url).origin === self.location.origin);
        if (open) { return open.navigate(url).then(w => (w || open).focus()).catch(() => self.clients.openWindow(url)); }
        return self.clients.openWindow(url);
    }));
});
