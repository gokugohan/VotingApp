# Results-night load test

Simulates results night against a disposable copy of the stack:

- **public**: visitors ramp to 200 concurrent users and refresh the dashboard (four JSON calls), national and municipality results pages, summary JSON and the CSV, with 1–3 s think time.
- **clerks**: 50 clerks, each signed in with their own account, enter the remaining 560 polling station sheets through the real form (5–15 s to type each sheet).

Data (`seed.sql`): 13 municipalities, 1,560 polling stations, a 16-candidate presidential round in Counting, 1,000 approved sheets (16,000 vote rows) and 50 clerk accounts (`loadclerk01..50@example.test` / `LoadTest123`). The script refuses to run on a database that already has elections.

## Run

```bash
cp .env.example .env    # set SA_PASSWORD
docker compose -p votingapp-load -f docker-compose.yml -f loadtest/docker-compose.loadtest.yml up -d --build
docker cp loadtest/seed.sql votingapp-load-db-1:/tmp/seed.sql
docker exec votingapp-load-db-1 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SA_PASSWORD" -d VotingApp -b -i /tmp/seed.sql
docker run --rm --network votingapp-load_default -v "$PWD/loadtest:/scripts:ro" grafana/k6:0.54.0 run /scripts/results-night.js
docker compose -p votingapp-load down -v
```

Options: `-e PUBLIC_VUS=500 -e HOLD=10m`. The override file only raises the per-IP login limit (all virtual users share one IP); everything else, including logging, is as in production.

## Pass criteria (k6 thresholds)

| Metric | Threshold |
|---|---|
| Failed requests | < 1 % |
| Public pages and JSON | p95 < 800 ms, p99 < 2 s |
| Sheet form and sheet save | p95 < 1.5 s |
| Sign-in (password hashing, once per shift) | p95 < 5 s |
| Checks (status codes, saves) | > 99 % |

Run it on hardware comparable to production; results from a developer laptop running app, database and load generator together are a lower bound.

## Results (developer laptop: app, SQL Server and k6 on one machine)

| Run | Public p95 | Sheet save p95 | Throughput | Failed requests |
|---|---|---|---|---|
| Before results cache (2026-11-09) | 19.3 s | 8.5 s | 54 req/s | 0.01 % |
| After single-flight, stale-while-revalidate cache | 76 ms | 158 ms | 153 req/s | 0 % |
| Redesigned UI, per-election station list, shared sheet index (2026-11-10) | 18 ms | 58 ms | 156 req/s | 0 % (sign-in p95 635 ms) |

The first run found cache stampedes (every request rebuilt expired aggregates at once) and uncached
municipality pages and CSV. `RoundResultsCache` fixed both: one rebuild per item at a time, other
requests served the previous value meanwhile, invalidated when sheets are approved or reopened.
