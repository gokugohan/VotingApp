<h1 align="center">Voting App Demo</h1>
<h3 align="center">"There is no one who loves pain itself, who seeks after it and wants to have it, simply because it is pain..."</h3>

- 📫 How to reach me **hchebre@gmail.com**

<h3 align="left">Connect with me:</h3>
<p align="left">
<a href="https://fb.com/helderchebre" target="blank"><img align="center" src="https://raw.githubusercontent.com/rahuldkjain/github-profile-readme-generator/master/src/images/icons/Social/facebook.svg" alt="helderchebre" height="30" width="40" /></a>
</p>


<p><img align="center" src="https://github-readme-stats.vercel.app/api/top-langs?username=gokugohan&show_icons=true&locale=en&layout=compact" alt="gokugohan" /></p>

<p><img align="center" src="https://github-readme-streak-stats.herokuapp.com/?user=gokugohan&" alt="gokugohan" /></p>

<img align="center" style="width:100%" src="https://user-images.githubusercontent.com/4695463/216550578-c733704f-d3a1-4bc7-a825-8d1ba8a4c92f.png"/>

## Architecture

| Project | Responsibility | Depends on |
|---|---|---|
| `VotingApp.Domain` | Entities, election rules (presidential majority/runoff, D'Hondt with threshold, result-sheet checks, gender quota) | nothing |
| `VotingApp.Application` | Use cases as services (candidates, polling stations, vote entry, results workflow, public results), permissions, `IAppDbContext` port | Domain |
| `VotingApp.Infrastructure` | EF Core / SQL Server context with audit logging, migrations, Identity user and seeding | Application |
| `VotingApp` (web) | MVC controllers and views, Identity UI, authorization, observability, hosting | Infrastructure |
| `VotingApp.Tests` | Rules and workflow tests (EF in-memory) | Infrastructure |

Package versions are managed centrally in `Directory.Packages.props`.

## Results workflow

1. **Set up** (*Elections → New election*): presidential or parliamentary, contestants, then move the round to **Counting**.
   Parliamentary elections need each party's closed list first (*Edit list* on the election page; paste one person per line as `Name, F` or `Name, M`). Lists must have at least one woman in every three effective positions; drafts can be saved, but the election cannot open until every list passes. Lists are fixed once voting starts and are public from then on.
2. **Enter**: a clerk types a polling station's paper sheet (ballot reconciliation, blank/invalid, votes per contestant). Arithmetic errors are flagged and block approval.
3. **Verify**: a *different* person re-types the sheet blind. Matching entries mark it Verified; differences are named for a supervisor.
4. **Approve**: municipal supervisor. Only approved/locked sheets count in the public results (`/Results/Round/{roundId}`, JSON at `/Results/Summary/{roundId}`).
5. **Lock**: national tabulation. Presidential runoffs are created from approved first-round results.
6. **Elected members** (parliamentary): each party's projected seats are filled from the top of its effective list and shown on the public results; a party that wins more seats than it has candidates is flagged.

**Parties over time.** Parties and coalitions have one lasting record (*Manage → Parties and coalitions*). When a party is put on a parliamentary ballot, pick its record (or type a new name and a record is created; names are matched ignoring case). A contestant can be re-linked later if a name was spelled differently. Results pages then show each contestant's change against the same round of the previous election of the same type (presidential candidates are matched through their candidate profile), and each party has a public history page.

**Polling stations per election.** When an election opens, it keeps its own copy of the polling station list (name, village, post, municipality). Stations added, moved, renamed or closed afterwards never change an election that is running or decided; they apply to elections that open later. A station cannot be deleted while an uncertified election uses it.

### Public results (open data)

| Page / endpoint | Content |
|---|---|
| `/Results/Round/{roundId}` | National provisional results, outcome (majority/runoff or seat projection), reporting %, table by municipality |
| `/Results/Municipality?roundId=…&id=TLS001` | Every polling station in a municipality with its published figures and a link to the photo/scan of the signed sheet |
| `/Results/Summary/{roundId}` | National summary (JSON) |
| `/Results/Municipalities/{roundId}` | Per-municipality results (JSON) |
| `/Results/Csv/{roundId}` | One row per counted station sheet (CSV, UTF-8) |

The home page shows the same approved figures for the most recent round in counting or later, with a selector for other rounds. Results pages refresh themselves: they poll a few-byte signature (`/Results/Signature/{roundId}`) every 20 seconds and re-load only when results changed.

The original quick vote entry (one total per candidate per station) has been removed in favour of result sheets. Its data remains in the `Votes` table and was copied into the "Imported results" election by the `AddElectionModel` migration.

Only approved and locked sheets are published; stations still being checked show their status only. Sheet scans (JPEG/PNG/PDF, checked by file signature, max 8 MB) are stored under `wwwroot/uploads/sheets`.

Admins configure roles under *Elections → Users and roles* (suggested: Clerk = enter, Verifier = verify, Municipal supervisor = verify + approve, National tabulation = approve + lock) and limit staff to municipalities under *User municipalities*.

## User interface

Server-rendered Razor pages with one hand-written stylesheet (`wwwroot/css/app.css`) and a small script (`wwwroot/js/site.js`); no CSS framework, chart library or web fonts, so pages stay light on slow and expensive mobile connections.

- **Accessible**: WCAG AA contrast, visible focus, keyboard navigation, 44 px touch targets, skip link, reduced-motion support, tables as the alternative to every chart.
- **Light and dark** themes (follows the device, with a toggle).
- **Charts without a library**: contestant results are sorted HTML bars (one series, one colour, 50% majority marker for presidential rounds); the municipality map is SVG drawn on the server from `wwwroot/data/tl-all.topo.json`; parliament seats are a dot grid.
- **Neutral colours**: navy and blue interface; contestant colours come from a colourblind-validated palette assigned in ballot order (never by rank, never party colours), at most 3 on the map and 8 for seats, with the rest shown as "Other" and named in the legend.

## Running locally

Requires the .NET 8 SDK and SQL Server (Express or LocalDB).

Connection strings and the first admin account are **not** stored in the repo. Use user-secrets in development:

```bash
dotnet user-secrets --project VotingApp set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=VotingApp;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
dotnet user-secrets --project VotingApp set "Seed:AdminEmail" "admin@example.org"      # optional: creates the first admin
dotnet user-secrets --project VotingApp set "Seed:AdminPassword" "<strong password>"
dotnet tool restore
dotnet ef database update --project VotingApp.Infrastructure --startup-project VotingApp
dotnet run --project VotingApp
dotnet test
```

New migrations: `dotnet ef migrations add <Name> --project VotingApp.Infrastructure --startup-project VotingApp --output-dir Persistence/Migrations`.

## Running with Docker

```bash
cp .env.example .env        # set SA_PASSWORD (and optionally ADMIN_EMAIL / ADMIN_PASSWORD)
docker compose up --build   # http://localhost:8080
```

The compose stack applies migrations on start-up (`Database__MigrateOnStartup=true`); in production prefer applying the idempotent SQL script produced by CI.

## Operations

| Concern | How |
|---|---|
| Configuration | Environment variables, e.g. `ConnectionStrings__DefaultConnection`, `Seed__AdminEmail`, `RateLimiting__LoginPerMinute` |
| Logs | Serilog, JSON to stdout in production; every line carries `CorrelationId` (send `X-Correlation-Id` to propagate yours) |
| Health | `/health/live` (process up), `/health/ready` (database reachable); the image's `HEALTHCHECK` uses `/health/live` |
| Several instances | Set `DataProtection__KeysPath` to shared storage so cookies/antiforgery tokens work across instances; protect keys at rest (certificate or key vault) in production |
| Rate limits | Login: 10/min per IP; other POSTs: 120/min per user (configurable) |
| Database resilience | SQL transient-fault retries enabled |

## CI

A results-night load test (public visitors plus clerks entering sheets, with pass/fail thresholds) lives in [`loadtest/`](loadtest/README.md).

`.github/workflows/ci.yml` builds and tests, fails on vulnerable NuGet packages, checks that migrations match the model, publishes an idempotent migration SQL script as an artifact, builds the container image on every PR and pushes it to `ghcr.io` on `master` and tags. Dependabot keeps NuGet, Actions and the base image current.
