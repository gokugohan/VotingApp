// Offline result entry. Sheets are queued in localStorage (per user and round) and sent to the server
// when a connection is available. The server applies each submission id at most once, so resending
// after a flaky connection is safe. All names are inserted with textContent (never as HTML).
(function () {
    "use strict";

    var app = document.getElementById("offline-app");
    if (!app) return;
    var pack = JSON.parse(document.getElementById("offline-pack").textContent);
    var syncUrl = app.getAttribute("data-sync-url");
    var canEnter = app.getAttribute("data-can-enter") === "true";
    var canVerify = app.getAttribute("data-can-verify") === "true";
    var token = (app.querySelector('input[name="__RequestVerificationToken"]') || {}).value;
    var storageKey = "votingapp.offline." + app.getAttribute("data-user") + "." + pack.roundId;

    var form = document.getElementById("sheet-form");
    var stationSelect = document.getElementById("station");
    var figuresBox = document.getElementById("figures");
    var liveCheck = document.getElementById("live-check");
    var rows = document.getElementById("queue-rows");
    var summary = document.getElementById("queue-summary");
    var netDot = document.getElementById("net-dot");
    var syncButton = document.getElementById("sync-now");
    var syncing = false;

    // ---------- storage ----------
    function load() { try { return JSON.parse(localStorage.getItem(storageKey) || "[]"); } catch (e) { return []; } }
    function store(queue) {
        try { localStorage.setItem(storageKey, JSON.stringify(queue)); return true; }
        catch (e) { alert("This device could not save the sheet (storage full or blocked)."); return false; }
    }
    function uuid() {
        if (window.crypto && crypto.randomUUID) return crypto.randomUUID();
        var b = new Uint8Array(16); crypto.getRandomValues(b);
        b[6] = (b[6] & 0x0f) | 0x40; b[8] = (b[8] & 0x3f) | 0x80;
        var h = Array.prototype.map.call(b, function (x) { return (x + 0x100).toString(16).slice(1); }).join("");
        return h.slice(0, 8) + "-" + h.slice(8, 12) + "-" + h.slice(12, 16) + "-" + h.slice(16, 20) + "-" + h.slice(20);
    }
    function el(tag, attrs, text) {
        var e = document.createElement(tag);
        Object.keys(attrs || {}).forEach(function (k) { e.setAttribute(k, attrs[k]); });
        if (text != null) e.textContent = text;
        return e;
    }

    // ---------- station picker ----------
    var stations = {};
    var byMunicipality = {};
    pack.stations.forEach(function (s) {
        stations[s.id] = s;
        (byMunicipality[s.municipality] = byMunicipality[s.municipality] || []).push(s);
    });
    stationSelect.appendChild(el("option", { value: "" }, "Choose a polling station…"));
    Object.keys(byMunicipality).sort().forEach(function (m) {
        var group = el("optgroup", { label: m });
        byMunicipality[m].forEach(function (s) {
            var stage = s.stage === "entered" ? " (awaiting verification)" : "";
            group.appendChild(el("option", { value: s.id }, s.name + " · " + s.village + stage));
        });
        stationSelect.appendChild(group);
    });
    if (pack.stations.length === 0) {
        stationSelect.appendChild(el("option", { value: "", disabled: "disabled" }, "No stations need entry in your municipalities"));
    }

    var kindInputs = form.querySelectorAll('input[name="kind"]');
    kindInputs.forEach(function (i) {
        if ((i.value === "entry" && !canEnter) || (i.value === "verify" && !canVerify)) { i.disabled = true; i.parentElement.style.opacity = ".5"; }
    });
    stationSelect.addEventListener("change", function () {
        var s = stations[stationSelect.value];
        var preferred = s && s.stage === "entered" && canVerify ? "verify" : (canEnter ? "entry" : "verify");
        kindInputs.forEach(function (i) { i.checked = i.value === preferred && !i.disabled; });
    });

    // ---------- figure inputs ----------
    function numberField(id, label, cls, required) {
        var wrap = el("div", { "class": "field", style: "grid-template-columns:1fr 9rem;align-items:center;margin-bottom:.6rem" });
        wrap.appendChild(el("label", { "for": id }, label));
        var input = el("input", { id: id, type: "number", min: "0", inputmode: "numeric", "class": cls || "" });
        if (required) input.required = true;
        wrap.appendChild(input);
        return wrap;
    }
    figuresBox.appendChild(el("h3", { style: "margin-top:1rem" }, "Ballots"));
    figuresBox.appendChild(numberField("f-registered", "Registered voters (if on the sheet)", "", false));
    figuresBox.appendChild(numberField("f-received", "Ballots received", "js-recv", true));
    figuresBox.appendChild(numberField("f-used", "Ballots used (in the box)", "js-used", true));
    figuresBox.appendChild(numberField("f-spoiled", "Spoiled ballots", "js-spoiled", true));
    figuresBox.appendChild(numberField("f-unused", "Unused ballots", "js-unused", true));
    figuresBox.appendChild(el("h3", { style: "margin-top:1rem" }, "Votes"));
    pack.ballot.forEach(function (c) {
        var f = numberField("f-c-" + c.id, c.name + (c.acronym ? " (" + c.acronym + ")" : ""), "js-vote", true);
        f.querySelector("input").setAttribute("data-contestant", c.id);
        figuresBox.appendChild(f);
    });
    figuresBox.appendChild(numberField("f-blank", "Blank votes", "js-vote", true));
    figuresBox.appendChild(numberField("f-invalid", "Invalid votes", "js-vote", true));

    function n(id) { var v = parseInt(document.getElementById(id).value, 10); return isNaN(v) ? null : v; }
    function check() {
        var votes = 0, missing = false;
        form.querySelectorAll(".js-vote").forEach(function (i) { var v = parseInt(i.value, 10); if (isNaN(v)) missing = true; else votes += v; });
        var used = n("f-used"), recv = n("f-received"), spoiled = n("f-spoiled"), unused = n("f-unused");
        var problems = [];
        if (!missing && used !== null && votes !== used) problems.push("Votes add up to " + votes + ", but ballots used is " + used + ".");
        if (recv !== null && used !== null && spoiled !== null && unused !== null && recv !== used + spoiled + unused)
            problems.push("Ballots received (" + recv + ") is not used + spoiled + unused (" + (used + spoiled + unused) + ").");
        liveCheck.style.color = problems.length ? "var(--bad-text)" : (missing || used === null ? "var(--muted)" : "var(--ok-text)");
        liveCheck.textContent = problems.length ? problems.join(" ") : (missing || used === null ? "Enter every figure from the sheet." : "Figures add up.");
    }
    form.addEventListener("input", check);
    form.addEventListener("reset", function () { setTimeout(check, 0); });
    check();

    // ---------- save to device ----------
    form.addEventListener("submit", function (e) {
        e.preventDefault();
        var station = stations[stationSelect.value];
        var kind = (form.querySelector('input[name="kind"]:checked') || {}).value;
        var required = form.querySelectorAll("input[required]");
        var empty = Array.prototype.filter.call(required, function (i) { return i.value === "" || parseInt(i.value, 10) < 0; });
        if (!station || !kind || empty.length) {
            liveCheck.style.color = "var(--bad-text)";
            liveCheck.textContent = !station ? "Choose a polling station." : !kind ? "Choose first entry or verification." : "Every figure is required (enter 0 if none).";
            (empty[0] || stationSelect).focus();
            return;
        }
        var votes = {};
        form.querySelectorAll("input[data-contestant]").forEach(function (i) { votes[i.getAttribute("data-contestant")] = parseInt(i.value, 10); });
        var item = {
            id: uuid(), kind: kind, roundId: pack.roundId, stationId: station.id,
            stationLabel: station.name + " · " + station.village, queuedAt: new Date().toISOString(),
            figures: {
                registeredVoters: n("f-registered"), ballotsReceived: n("f-received"), ballotsUsed: n("f-used"),
                ballotsSpoiled: n("f-spoiled"), ballotsUnused: n("f-unused"), blankVotes: n("f-blank"), invalidVotes: n("f-invalid"),
                votes: votes
            },
            status: "pending", message: "Waiting to be sent."
        };
        var queue = load(); queue.push(item);
        if (!store(queue)) return;
        form.reset(); stationSelect.focus();
        render();
        if (navigator.onLine) sync();
    });

    // ---------- queue list ----------
    var labels = { pending: ["", "Waiting"], saved: ["badge-ok", "Saved"], verified: ["badge-ok", "Verified"], mismatch: ["badge-warn", "Entries differ"], rejected: ["badge-bad", "Rejected"] };
    function render() {
        var queue = load();
        rows.textContent = "";
        queue.slice().reverse().forEach(function (item) {
            var tr = el("tr");
            var who = el("td");
            who.appendChild(el("strong", null, item.stationLabel));
            who.appendChild(el("div", { "class": "muted small" }, (item.kind === "verify" ? "Verification" : "First entry") + " · saved " + new Date(item.queuedAt).toLocaleString()));
            tr.appendChild(who);
            var st = el("td");
            var l = labels[item.status] || labels.pending;
            st.appendChild(el("span", { "class": "badge " + l[0] }, l[1]));
            st.appendChild(el("div", { "class": "small" }, item.message || ""));
            tr.appendChild(st);
            var act = el("td", { "class": "actions-cell" });
            if (item.status === "pending" || item.status === "rejected") {
                var rm = el("button", { type: "button", "class": "btn btn-ghost btn-sm" }, "Remove");
                rm.addEventListener("click", function () {
                    if (item.status === "pending" && !confirm("Remove this sheet before it is sent?")) return;
                    store(load().filter(function (x) { return x.id !== item.id; })); render();
                });
                act.appendChild(rm);
            }
            tr.appendChild(act);
            rows.appendChild(tr);
        });
        if (!queue.length) {
            var empty = el("tr"); empty.appendChild(el("td", { colspan: "3", "class": "empty" }, "No sheets saved on this device yet.")); rows.appendChild(empty);
        }
        var pending = queue.filter(function (x) { return x.status === "pending"; }).length;
        summary.textContent = pending ? pending + " sheet(s) waiting to be sent." : "Nothing waiting to be sent.";
        syncButton.disabled = !pending || syncing;
    }
    document.getElementById("clear-done").addEventListener("click", function () {
        store(load().filter(function (x) { return x.status === "pending" || x.status === "rejected"; })); render();
    });

    // ---------- sync ----------
    function setNet() {
        netDot.className = "badge " + (navigator.onLine ? "badge-ok" : "badge-warn");
        netDot.textContent = navigator.onLine ? "Online" : "Offline: sheets are kept on this device";
    }
    function sync() {
        var pending = load().filter(function (x) { return x.status === "pending"; }).slice(0, 25);
        if (!pending.length || syncing || !navigator.onLine) { render(); return; }
        syncing = true; render();
        summary.textContent = "Sending " + pending.length + " sheet(s)…";
        fetch(syncUrl, {
            method: "POST", credentials: "same-origin",
            headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
            body: JSON.stringify(pending.map(function (x) { return { id: x.id, kind: x.kind, roundId: x.roundId, stationId: x.stationId, queuedAt: x.queuedAt, figures: x.figures }; }))
        }).then(function (res) {
            var json = (res.headers.get("content-type") || "").indexOf("application/json") >= 0;
            if (res.redirected || res.status === 401 || res.status === 403 || (res.ok && !json)) {
                throw new Error("Your session has ended. Sign in again while online to send the waiting sheets; they stay saved here.");
            }
            if (res.status === 400) throw new Error("The server could not read the sheets. Please reload the page.");
            if (!res.ok) throw new Error("The server is not available right now; sheets stay saved here and will be sent later.");
            return res.json();
        }).then(function (outcomes) {
            var byId = {};
            outcomes.forEach(function (o) { byId[o.id] = o; });
            var queue = load().map(function (x) {
                var o = byId[x.id];
                if (!o) return x;
                x.status = o.outcome; x.message = (o.message || "") + (o.replayed ? " (already received earlier)" : "");
                return x;
            });
            store(queue);
        }).catch(function (err) {
            summary.textContent = err.message || "Could not reach the server; will try again.";
        }).then(function () {
            syncing = false; render();
            if (load().some(function (x) { return x.status === "pending"; }) && navigator.onLine) setTimeout(sync, 5000);
        });
    }

    syncButton.addEventListener("click", sync);
    window.addEventListener("online", function () { setNet(); sync(); });
    window.addEventListener("offline", setNet);
    setInterval(function () { if (navigator.onLine) sync(); }, 60000);
    setNet(); render();
    if (navigator.onLine) sync();

    // Cache this page and its assets so it opens without a connection.
    if ("serviceWorker" in navigator) {
        navigator.serviceWorker.register("/sw.js").catch(function () { /* page still works while it stays open */ });
    }
})();
