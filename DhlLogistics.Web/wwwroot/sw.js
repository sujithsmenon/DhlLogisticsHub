// DHL Logistics Hub — Service Worker
// Handles web push notifications when the browser tab is closed or backgrounded.

const CACHE_NAME = 'dhl-logistics-v1';

// ── Push event ───────────────────────────────────────────────────────────────
self.addEventListener('push', event => {
    if (!event.data) return;

    let payload;
    try { payload = event.data.json(); }
    catch { payload = { title: 'DHL Logistics', body: event.data.text(), url: '/' }; }

    const options = {
        body:    payload.body,
        icon:    '/favicon.png',
        badge:   '/favicon.png',
        tag:     payload.type || 'dhl-notification',
        renotify: true,
        data:    { url: payload.url || '/' },
        actions: [
            { action: 'open',    title: 'Open'    },
            { action: 'dismiss', title: 'Dismiss' },
        ],
    };

    event.waitUntil(
        self.registration.showNotification(payload.title, options)
    );
});

// ── Notification click ───────────────────────────────────────────────────────
self.addEventListener('notificationclick', event => {
    event.notification.close();

    if (event.action === 'dismiss') return;

    const url = event.notification.data?.url || '/';

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(windowClients => {
            // Focus existing tab if open
            for (const client of windowClients) {
                if (client.url.includes(self.location.origin) && 'focus' in client) {
                    client.navigate(url);
                    return client.focus();
                }
            }
            // Otherwise open a new tab
            if (clients.openWindow) return clients.openWindow(url);
        })
    );
});

// ── Install / activate ───────────────────────────────────────────────────────
self.addEventListener('install',  () => self.skipWaiting());
self.addEventListener('activate', e => e.waitUntil(clients.claim()));
