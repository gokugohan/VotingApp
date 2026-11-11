// Site behaviour: small, dependency-free, progressive enhancement only.
(function () {
    "use strict";

    // ---- Theme toggle: system -> light -> dark ----
    var root = document.documentElement;
    var order = ["system", "light", "dark"];
    function currentTheme() { return root.getAttribute("data-theme") || "system"; }
    function applyTheme(theme) {
        if (theme === "system") root.removeAttribute("data-theme"); else root.setAttribute("data-theme", theme);
        try { if (theme === "system") localStorage.removeItem("theme"); else localStorage.setItem("theme", theme); } catch (e) { }
        document.querySelectorAll(".theme-toggle").forEach(function (btn) {
            btn.setAttribute("aria-label", (btn.getAttribute("data-label") || "Colour theme") + ": " + (btn.getAttribute("data-" + theme) || theme));
            btn.querySelectorAll("[data-theme-icon]").forEach(function (icon) {
                icon.hidden = icon.getAttribute("data-theme-icon") !== theme;
            });
        });
    }
    document.querySelectorAll(".theme-toggle").forEach(function (btn) {
        btn.addEventListener("click", function () {
            applyTheme(order[(order.indexOf(currentTheme()) + 1) % order.length]);
        });
    });
    applyTheme(currentTheme());

    // ---- Mobile navigation ----
    var toggle = document.querySelector(".nav-toggle");
    var nav = document.getElementById("site-nav");
    if (toggle && nav) {
        toggle.addEventListener("click", function () {
            var open = nav.classList.toggle("open");
            toggle.setAttribute("aria-expanded", open ? "true" : "false");
        });
    }

    // Close other open menus when one opens; close on Escape.
    var menus = document.querySelectorAll(".nav details");
    menus.forEach(function (d) {
        d.addEventListener("toggle", function () {
            if (d.open) menus.forEach(function (o) { if (o !== d) o.open = false; });
        });
    });
    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape") menus.forEach(function (d) { d.open = false; });
    });

    // ---- Signing out clears pages saved for offline entry (queued sheets stay with the offline page) ----
    document.querySelectorAll('form[action*="Logout"]').forEach(function (form) {
        form.addEventListener("submit", function () {
            if (navigator.serviceWorker && navigator.serviceWorker.controller) navigator.serviceWorker.controller.postMessage("sign-out");
        });
    });

    // ---- Confirmation for destructive or irreversible forms: <form data-confirm="..."> ----
    document.querySelectorAll("form[data-confirm]").forEach(function (form) {
        form.addEventListener("submit", function (e) {
            if (!window.confirm(form.getAttribute("data-confirm"))) e.preventDefault();
        });
    });

    // ---- Prompt for a reason: <form data-prompt="question"> with <input name="reason"> ----
    document.querySelectorAll("form[data-prompt]").forEach(function (form) {
        form.addEventListener("submit", function (e) {
            var answer = window.prompt(form.getAttribute("data-prompt"));
            if (!answer || !answer.trim()) { e.preventDefault(); return; }
            form.querySelector("input[name=reason]").value = answer.trim();
        });
    });

    // ---- Prevent double submission: disable the submit button while posting ----
    document.querySelectorAll("form[method=post]").forEach(function (form) {
        form.addEventListener("submit", function (e) {
            if (e.defaultPrevented) return;
            var btn = form.querySelector("button[type=submit]:not([formnovalidate])");
            if (btn && !form.hasAttribute("data-no-disable")) {
                setTimeout(function () { btn.disabled = true; btn.setAttribute("aria-busy", "true"); }, 0);
            }
        });
    });

    // ---- Live results: <div data-live="signature-url" data-live-signature="..."> ----
    // Polls a tiny signature while the page is visible; when results change, re-fetches the page
    // and swaps the region in place. No WebSocket, no library: cheap on slow connections.
    // Browsers have no Tetun locale; Tetun pages use Portuguese conventions, like the server.
    function locale() { var l = document.documentElement.lang || "en"; return l === "tet" ? "pt-PT" : l === "pt" ? "pt-PT" : "en-GB"; }

    document.querySelectorAll("[data-live]").forEach(function (region) {
        var url = region.getAttribute("data-live");
        var signature = region.getAttribute("data-live-signature");
        var status = document.querySelector("[data-live-status]");
        var busy = false;
        function setStatus(text) { if (status) status.textContent = text; }
        function tick() {
            if (document.hidden || busy) return;
            busy = true;
            fetch(url, { cache: "no-store" })
                .then(function (r) { return r.ok ? r.text() : signature; })
                .then(function (next) {
                    if (next === signature) return null;
                    return fetch(window.location.href, { cache: "no-store" }).then(function (r) { return r.text(); }).then(function (html) {
                        var doc = new DOMParser().parseFromString(html, "text/html");
                        var fresh = doc.querySelector("[data-live]");
                        if (!fresh) return;
                        region.innerHTML = fresh.innerHTML;
                        signature = fresh.getAttribute("data-live-signature");
                        setStatus((status && status.getAttribute("data-live-updated") || "Results updated {0}").replace("{0}", new Date().toLocaleTimeString(locale())));
                    });
                })
                .catch(function () { /* offline: try again next tick */ })
                .then(function () { busy = false; });
        }
        setInterval(tick, 20000);
        document.addEventListener("visibilitychange", function () { if (!document.hidden) tick(); });
    });
})();
