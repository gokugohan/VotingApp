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
2. **Enter**: a clerk types a polling station's paper sheet (ballot reconciliation, blank/invalid, votes per contestant). Arithmetic errors are flagged and block approval.
3. **Verify**: a *different* person re-types the sheet blind. Matching entries mark it Verified; differences are named for a supervisor.
4. **Approve**: municipal supervisor. Only approved/locked sheets count in the public results (`/Results/Round/{roundId}`, JSON at `/Results/Summary/{roundId}`).
5. **Lock**: national tabulation. Presidential runoffs are created from approved first-round results.

Admins configure roles under *Elections → Users and roles* (suggested: Clerk = enter, Verifier = verify, Municipal supervisor = verify + approve, National tabulation = approve + lock) and limit staff to municipalities under *User municipalities*.

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

`.github/workflows/ci.yml` builds and tests, fails on vulnerable NuGet packages, checks that migrations match the model, publishes an idempotent migration SQL script as an artifact, builds the container image on every PR and pushes it to `ghcr.io` on `master` and tags. Dependabot keeps NuGet, Actions and the base image current.
