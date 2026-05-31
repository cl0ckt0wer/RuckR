// Production service worker for versioned static assets only.
// Dynamic app shell, config, auth, API, and boot metadata stay network-first.
self.importScripts('./service-worker-assets.js');

const cacheNamePrefix = 'ruckr-static-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const staticPathPrefixes = [
    '/_framework/',
    '/_content/',
    '/css/',
    '/images/',
    '/img/'
];
const rootStaticAssetPaths = [
    '/ruckr.client.styles.css'
];
const dynamicPathPrefixes = [
    '/api/',
    '/identity/',
    '/healthz',
    '/jaeger',
    '/.well-known'
];
const cacheableExtensions = [
    '.wasm',
    '.dll',
    '.dat',
    '.blat',
    '.js',
    '.css',
    '.png',
    '.jpg',
    '.jpeg',
    '.gif',
    '.webp',
    '.svg',
    '.ico',
    '.woff',
    '.woff2',
    '.ttf',
    '.otf'
];
let manifestCacheableUrlSet;

function toUrl(url) {
    try {
        return new URL(url, self.location.origin);
    } catch {
        return null;
    }
}

function isSameOriginStaticUrl(url) {
    return url.origin === self.location.origin
        && (staticPathPrefixes.some(prefix => url.pathname.toLowerCase().startsWith(prefix))
            || rootStaticAssetPaths.includes(url.pathname.toLowerCase()));
}

function hasCacheableExtension(pathname) {
    const lowerPath = pathname.toLowerCase();
    return cacheableExtensions.some(extension => lowerPath.endsWith(extension));
}

function isDynamicPath(pathname) {
    const lowerPath = pathname.toLowerCase();
    return dynamicPathPrefixes.some(prefix => lowerPath.startsWith(prefix));
}

function isExcludedStaticAsset(pathname) {
    const lowerPath = pathname.toLowerCase();
    const fileName = lowerPath.split('/').pop() ?? '';

    return lowerPath === '/'
        || lowerPath.endsWith('/index.html')
        || lowerPath.endsWith('/manifest.json')
        || lowerPath.endsWith('/_framework/blazor.boot.json')
        || fileName === 'service-worker.js'
        || fileName === 'service-worker.published.js'
        || fileName === 'service-worker-assets.js'
        || /^appsettings(\.[a-z0-9_-]+)?\.json$/i.test(fileName);
}

function shouldCacheStaticUrl(url) {
    if (url.search) return false;
    if (!isSameOriginStaticUrl(url)) return false;
    if (isDynamicPath(url.pathname)) return false;
    if (isExcludedStaticAsset(url.pathname)) return false;
    return hasCacheableExtension(url.pathname);
}

function getManifestCacheableUrlSet() {
    if (manifestCacheableUrlSet) {
        return manifestCacheableUrlSet;
    }

    manifestCacheableUrlSet = new Set(self.assetsManifest.assets
        .map(asset => toUrl(asset.url))
        .filter(url => url && shouldCacheStaticUrl(url))
        .map(url => url.href));

    return manifestCacheableUrlSet;
}

function shouldCacheAssetUrl(assetUrl) {
    const url = toUrl(assetUrl);
    if (!url || !shouldCacheStaticUrl(url)) return false;
    return getManifestCacheableUrlSet().has(url.href);
}

function shouldPrecacheAssetUrl(assetUrl) {
    const url = toUrl(assetUrl);
    if (!url || !shouldCacheAssetUrl(url.href)) return false;

    const pathname = url.pathname.toLowerCase();
    return pathname.startsWith('/_framework/')
        || pathname.startsWith('/css/')
        || pathname === '/ruckr.client.styles.css'
        || pathname.startsWith('/_content/mudblazor/')
        || (pathname.startsWith('/_content/dymaptic.geoblazor.core/') && pathname.endsWith('.css'));
}

function shouldHandleFetchRequestUrl(requestUrl, method = 'GET') {
    if (method !== 'GET') return false;

    const url = toUrl(requestUrl);
    if (!url) return false;
    if (url.search) return false;
    if (isDynamicPath(url.pathname)) return false;
    return shouldCacheAssetUrl(url.href);
}

self.RuckRServiceWorkerCachePolicy = {
    shouldCacheAssetUrl,
    shouldHandleFetchRequestUrl
};

self.addEventListener('install', event => {
    event.waitUntil((async () => {
        const cache = await caches.open(cacheName);
        const requests = self.assetsManifest.assets
            .filter(asset => shouldPrecacheAssetUrl(asset.url))
            .map(asset => new Request(asset.url, {
                integrity: asset.hash,
                cache: 'no-cache'
            }));

        await cache.addAll(requests);
        await self.skipWaiting();
    })());
});

self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        const cacheKeys = await caches.keys();
        await Promise.all(cacheKeys
            .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
            .map(key => caches.delete(key)));

        await self.clients.claim();
    })());
});

self.addEventListener('fetch', event => {
    if (!shouldHandleFetchRequestUrl(event.request.url, event.request.method)) {
        return;
    }

    event.respondWith((async () => {
        const cachedResponse = await caches.match(event.request);
        if (cachedResponse) {
            return cachedResponse;
        }

        const response = await fetch(event.request);
        if (response.ok) {
            const cache = await caches.open(cacheName);
            await cache.put(event.request, response.clone());
        }

        return response;
    })());
});
