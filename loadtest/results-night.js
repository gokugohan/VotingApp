// Results-night load test (k6). See loadtest/README.md.
//
// Two scenarios run together against a database seeded with loadtest/seed.sql:
//   public  - visitors refreshing the dashboard, results pages, JSON and CSV (ramps to PUBLIC_VUS)
//   clerks  - 50 clerks entering the remaining polling station sheets through the real form
import http from 'k6/http';
import exec from 'k6/execution';
import { check, sleep, fail } from 'k6';
import { Trend } from 'k6/metrics';

const BASE = __ENV.BASE_URL || 'http://app:8080';
const PUBLIC_VUS = parseInt(__ENV.PUBLIC_VUS || '200', 10);
const HOLD = __ENV.HOLD || '3m';
const CLERKS = 50;
const PASSWORD = 'LoadTest123';

const sheetSave = new Trend('sheet_save_duration', true);

export const options = {
    scenarios: {
        public: {
            executor: 'ramping-vus',
            exec: 'publicVisitor',
            startVUs: 0,
            stages: [
                { duration: '1m', target: PUBLIC_VUS },
                { duration: HOLD, target: PUBLIC_VUS },
                { duration: '30s', target: 0 },
            ],
            gracefulRampDown: '10s',
        },
        clerks: {
            executor: 'shared-iterations',
            exec: 'clerk',
            vus: CLERKS,
            iterations: 560, // every remaining station once
            startTime: '30s',
            maxDuration: '6m',
        },
    },
    thresholds: {
        'http_req_failed': ['rate<0.01'],
        'http_req_duration{scenario:public}': ['p(95)<800', 'p(99)<2000'],
        // Sheet entry only. Sign-in is measured separately: password hashing (100,000 PBKDF2 rounds)
        // is deliberately slow and is paid once per shift, not per sheet.
        'http_req_duration{scenario:clerks,name:sheet-form}': ['p(95)<1500'],
        'http_req_duration{scenario:clerks,name:sheet-save}': ['p(95)<1500'],
        'http_req_duration{name:login}': ['p(95)<5000'],
        'sheet_save_duration': ['p(95)<1500'],
        'checks': ['rate>0.99'],
    },
};

function token(html) {
    const m = /name="__RequestVerificationToken" type="hidden" value="([^"]+)"/.exec(html);
    return m ? m[1] : null;
}

function login(email) {
    const page = http.get(`${BASE}/Identity/Account/Login`, { tags: { name: 'login-page' } });
    const res = http.post(`${BASE}/Identity/Account/Login`, {
        'Input.Email': email,
        'Input.Password': PASSWORD,
        '__RequestVerificationToken': token(page.body),
    }, { redirects: 0, tags: { name: 'login' } });
    return res.status === 302;
}

// Runs once: find the round, the stations without a sheet, and municipality ids.
export function setup() {
    const elections = http.get(`${BASE}/Elections`);
    const round = /\/Results\/Round\/([0-9a-f-]{36})/i.exec(elections.body);
    if (!round) fail('No election round found. Did seed.sql run?');
    const roundId = round[1];

    if (!login('loadclerk01@example.test')) fail('Clerk login failed (password hash or rate limit?)');
    const board = http.get(`${BASE}/ResultSheets?roundId=${roundId}`);
    const stations = [];
    const re = /ResultSheets\/Enter\?roundId=[0-9a-f-]{36}&amp;stationId=([0-9a-f-]{36})">Enter</gi;
    let m;
    while ((m = re.exec(board.body)) !== null) stations.push(m[1]);
    if (stations.length < 560) fail(`Expected 560 stations to enter, found ${stations.length}`);

    const municipalities = [];
    for (let i = 1; i <= 13; i++) municipalities.push('TLS' + String(i).padStart(3, '0'));
    return { roundId, stations, municipalities };
}

export function publicVisitor(data) {
    const r = data.roundId;
    const pick = Math.random();
    let res;
    if (pick < 0.20) {
        res = http.get(`${BASE}/?roundId=${r}`, { tags: { name: 'home' } });
    } else if (pick < 0.50) {
        // The dashboard page fires these four together.
        const batch = http.batch([
            ['GET', `${BASE}/Dashboard/Contestants/${r}`, null, { tags: { name: 'dash-contestants' } }],
            ['GET', `${BASE}/Dashboard/Totals/${r}`, null, { tags: { name: 'dash-totals' } }],
            ['GET', `${BASE}/Dashboard/ByMunicipality/${r}`, null, { tags: { name: 'dash-map' } }],
            ['GET', `${BASE}/Dashboard/Series/${r}`, null, { tags: { name: 'dash-series' } }],
        ]);
        res = batch[batch.length - 1];
        check(batch, { 'dashboard json 200': (b) => b.every((x) => x.status === 200) });
    } else if (pick < 0.70) {
        res = http.get(`${BASE}/Results/Round/${r}`, { tags: { name: 'results-round' } });
    } else if (pick < 0.80) {
        res = http.get(`${BASE}/Results/Summary/${r}`, { tags: { name: 'results-json' } });
    } else if (pick < 0.97) {
        const muni = data.municipalities[Math.floor(Math.random() * data.municipalities.length)];
        res = http.get(`${BASE}/Results/Municipality?roundId=${r}&id=${muni}`, { tags: { name: 'results-municipality' } });
    } else {
        res = http.get(`${BASE}/Results/Csv/${r}`, { tags: { name: 'results-csv' } });
    }
    check(res, { 'public 200': (x) => x.status === 200 });
    sleep(1 + Math.random() * 2); // think time
}

export function clerk(data) {
    const vu = exec.vu.idInTest;
    // Each VU signs in once with its own account (cookies persist per VU).
    if (exec.vu.iterationInScenario === 0) {
        sleep(Math.random() * 60); // clerks arrive over the first minute, not all in the same second
        const email = `loadclerk${String(((vu - 1) % CLERKS) + 1).padStart(2, '0')}@example.test`;
        if (!check(login(email), { 'clerk login': (ok) => ok })) return;
    }

    const station = data.stations[exec.scenario.iterationInTest];
    const form = http.get(`${BASE}/ResultSheets/Enter?roundId=${data.roundId}&stationId=${station}`, { tags: { name: 'sheet-form' } });
    if (!check(form, { 'sheet form 200': (x) => x.status === 200 })) return;

    const ids = [];
    const re = /name="Form\.Votes\[(\d+)\]\.ContestantId" value="([0-9a-fA-F-]{36})"/g;
    let m;
    while ((m = re.exec(form.body)) !== null) ids.push(m[2]);

    const fields = {
        '__RequestVerificationToken': token(form.body),
        'Form.RoundId': data.roundId,
        'Form.StationId': station,
    };
    let valid = 0;
    ids.forEach((id, i) => {
        const votes = Math.floor(Math.random() * (120 / (i + 1) + 5));
        valid += votes;
        fields[`Form.Votes[${i}].ContestantId`] = id;
        fields[`Form.Votes[${i}].Votes`] = String(votes);
    });
    const blank = 3, invalid = 2, used = valid + blank + invalid;
    Object.assign(fields, {
        'Form.RegisteredVoters': String(used + 100),
        'Form.BallotsReceived': String(used + 52),
        'Form.BallotsUsed': String(used),
        'Form.BallotsSpoiled': '2',
        'Form.BallotsUnused': '50',
        'Form.BlankVotes': String(blank),
        'Form.InvalidVotes': String(invalid),
    });

    sleep(5 + Math.random() * 10); // typing the sheet
    const save = http.post(`${BASE}/ResultSheets/Enter`, fields, { redirects: 0, tags: { name: 'sheet-save' } });
    sheetSave.add(save.timings.duration);
    check(save, { 'sheet saved (302)': (x) => x.status === 302 });
}
