// Development-stage production deploys should never serve stale Blazor config.
// Retire any previously installed offline worker and clear its caches.
self.addEventListener('install', event => {
    event.waitUntil(self.skipWaiting());
});

self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        const cacheKeys = await caches.keys();
        await Promise.all(cacheKeys.map(key => caches.delete(key)));

        const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        await Promise.all(clients.map(client => client.navigate(client.url)));

        await self.registration.unregister();
    })());
});

self.addEventListener('fetch', event => {
    event.respondWith(fetch(event.request));
});
