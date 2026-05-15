// DHL Logistics Hub — Web Push + Service Worker registration
// Called from MainLayout after first render.

window.DhlPush = (function () {

    let _swReg = null;

    async function init(hubUrl, baseUrl) {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
            console.log('DhlPush: Web Push not supported in this browser.');
            return;
        }

        try {
            // 1. Register service worker
            _swReg = await navigator.serviceWorker.register('/sw.js', { scope: '/' });
            console.log('DhlPush: service worker registered.');

            // 2. Ask notification permission
            const perm = await Notification.requestPermission();
            if (perm !== 'granted') { console.log('DhlPush: permission denied.'); return; }

            // 3. Fetch VAPID public key from server
            const keyRes = await fetch('/api/notifications/vapid-public-key');
            const vapidKey = await keyRes.text();
            if (!vapidKey || vapidKey.startsWith('GENERATE')) {
                console.log('DhlPush: VAPID keys not configured — skipping web push.');
                return;
            }

            // 4. Subscribe (or reuse existing subscription)
            let sub = await _swReg.pushManager.getSubscription();
            if (!sub) {
                sub = await _swReg.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: urlBase64ToUint8Array(vapidKey),
                });
            }

            // 5. Send subscription to server
            const jwt = getJwtFromStorage();
            if (!jwt) { console.log('DhlPush: no JWT, cannot register subscription.'); return; }

            const keys = sub.toJSON().keys;
            await fetch('/api/notifications/web-push/subscribe', {
                method:  'POST',
                headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${jwt}` },
                body:    JSON.stringify({ endpoint: sub.endpoint, p256dh: keys.p256dh, auth: keys.auth }),
            });
            console.log('DhlPush: web push subscription registered.');

        } catch (err) {
            console.error('DhlPush: init failed —', err);
        }
    }

    function urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64  = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        const raw     = atob(base64);
        return Uint8Array.from([...raw].map(c => c.charCodeAt(0)));
    }

    function getJwtFromStorage() {
        // Mirror what the mobile app does — JWT may be stored in localStorage by Blazor
        return localStorage.getItem('dhl_jwt') || sessionStorage.getItem('dhl_jwt') || null;
    }

    return { init };
})();
