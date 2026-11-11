// Service worker: lets the offline entry page open without a connection.
// - /OfflineEntry pages: network first, falling back to the last copy saved on this device.
// - Styles, scripts and icon: served from the device, refreshed in the background.
// Nothing else is cached, so results pages always come live from the server.
"use strict";

var CACHE = "votingapp-offline-v1";
var ASSETS = ["/css/app.css", "/js/site.js", "/js/offline.js", "/favicon.ico"];

self.addEventListener("install", function (event) {
    event.waitUntil(caches.open(CACHE).then(function (cache) { return cache.addAll(ASSETS); }).then(function () { return self.skipWaiting(); }));
});

self.addEventListener("activate", function (event) {
    event.waitUntil(caches.keys().then(function (keys) {
        return Promise.all(keys.filter(function (k) { return k !== CACHE; }).map(function (k) { return caches.delete(k); }));
    }).then(function () { return self.clients.claim(); }));
});

self.addEventListener("fetch", function (event) {
    var req = event.request;
    if (req.method !== "GET") return;
    var url = new URL(req.url);
    if (url.origin !== self.location.origin) return;

    if (req.mode === "navigate" && url.pathname.toLowerCase().indexOf("/offlineentry") === 0) {
        event.respondWith(fetch(req).then(function (res) {
            // Keep only real pages (not a redirect to sign-in) as the offline copy.
            if (res.ok && !res.redirected) {
                var copy = res.clone();
                caches.open(CACHE).then(function (cache) { cache.put(req, copy); });
            }
            return res;
        }).catch(function () {
            return caches.match(req).then(function (hit) {
                return hit || new Response("<h1>Offline</h1><p>Open this page once while online on this device to use it offline.</p>",
                    { headers: { "Content-Type": "text/html; charset=utf-8" } });
            });
        }));
        return;
    }

    var isAsset = ASSETS.some(function (a) { return url.pathname === a; });
    if (isAsset) {
        event.respondWith(caches.match(req, { ignoreSearch: true }).then(function (hit) {
            var refresh = fetch(req).then(function (res) {
                if (res.ok) {
                    var copy = res.clone(); // clone before the page consumes the body
                    caches.open(CACHE).then(function (cache) { cache.put(url.pathname, copy); });
                }
                return res;
            }).catch(function () { return hit; });
            return hit || refresh;
        }));
    }
});

// Signing out clears saved pages (they contain the user's stations); queued sheets are kept by the page.
self.addEventListener("message", function (event) {
    if (event.data === "sign-out") caches.delete(CACHE);
});
