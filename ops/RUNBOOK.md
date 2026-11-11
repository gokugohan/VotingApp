# Election runbook

For the people who run VotingApp for a presidential or parliamentary election: what to do before, on, and after results night. Commands assume the Docker Compose deployment from the README, run on the server from the folder that holds `docker-compose.yml`.

## Four weeks before

- **Passwords.** Choose new passwords for the SQL Server `sa` login and the first admin account; never reuse a password that has been in a file, a chat or the git history. See [Changing the database password](#changing-the-database-password).
- **A database login for the app.** The app should not connect as `sa`. Create a login that owns only the VotingApp database (see below) and put it in the connection string.
- **HTTPS.** Put the app behind a reverse proxy with a valid certificate. The app trusts `X-Forwarded-*` headers (`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`).
- **Keys.** The `dpkeys` volume holds the keys that sign sign-in cookies and form tokens. It is in every backup; keep it with the database.
- **Load test.** Run [the results-night load test](../loadtest/README.md) on the production hardware. It must meet its pass criteria.
- **Backups.** Schedule `ops/backup.sh` (below), copy the output off the server, and run a restore drill.

## One to two weeks before

- **Election set-up.** Create the election, add the contestants, link parties to their records, and enter and check every party list (gender rule, enough candidates). Opening the election fixes the polling station list, so check stations first.
- **Accounts.** One account per person, never shared. Give each a role (Clerk, Verifier, Municipal supervisor, National tabulation) and limit municipal staff to their municipalities. Remove accounts of people who left.
- **Offline entry.** Clerks in places with a poor connection sign in and open *Result sheets → Offline entry* once while online, on the device they will use.
- **Rehearsal.** Enter, verify and approve a few test sheets in a copy of the system, print a results record, and check the CSV fingerprint (`certutil -hashfile file.csv SHA256` or `sha256sum file.csv`).
- **Restore drill.** `ops/restore.sh --drill backups/<latest>` must end with "Drill passed".

## Results night

- **Backups every 30 minutes**, copied off the server. Example crontab line (as a user that can run docker):

  ```
  */30 * * * * cd /srv/votingapp && BACKUP_DIR=/srv/backups ops/backup.sh >> /var/log/votingapp-backup.log 2>&1
  ```

- **Watch.** `/health/ready` (database reachable) from outside the server; the *Problems* tab of each round's result sheets; the audit log for unexpected changes.
- **A wrong sheet was approved.** A supervisor reopens it from the result sheets list; it leaves the results until it is approved again. Every step is in the audit log.
- **An account may be compromised.** An admin removes its roles under *Users and roles*, then checks the audit log for what it changed.
- **The app stops responding.** `docker compose ps`, then `docker compose logs --tail 200 app`. `docker compose restart app` is safe: nothing is kept only in memory.
- **The database is lost or damaged.** Restore the latest good backup (below). Sheets saved after that backup must be entered again; the paper sheets are the reference.

## After results night

- **Provisional results.** Move the round to *Provisional*. Print the national and municipal results records, sign them, and publish them with the CSV; the fingerprint on the paper must match the published CSV.
- **Certification.** Once the results are final, move the round to *Certified* (this cannot be undone), print and sign the final records, and take a last backup that is kept with the election archive.
- **Seat changes** (parliamentary) are recorded on each party's *Seats* page as they happen.
- **Accounts.** Remove roles from temporary staff.

## Backups

`ops/backup.sh` writes `backups/<UTC time>/` with:

| File | Content |
|---|---|
| `VotingApp.bak` | Full database backup, with page checksums, verified after writing |
| `files.tar.gz` | Photos of signed sheets, candidate photos, data-protection keys |
| `counts.txt` | Elections, result sheets and audit entries at backup time |
| `SHA256SUMS` | Checksums of the three files above |

It keeps the newest 14 backups (`BACKUP_KEEP`). Settings: `ENV_FILE` (default `.env`), `BACKUP_DIR`, `COMPOSE_PROJECT_NAME`, `DB_NAME`.

## Restoring

```bash
ops/restore.sh --drill backups/20271101T120000Z   # practice: restores into a scratch database, compares counts, drops it
ops/restore.sh backups/20271101T120000Z           # real: stops the app, replaces the database and files, starts the app
```

Both refuse a backup whose checksums do not match. A real restore replaces the whole database. Sheet photos added after the backup are kept, but the database no longer refers to them. After a real restore, check `/health/ready`, sign in, and look at the latest audit log entries to see where the data stops.

## Changing the database password

1. Choose a new password (12+ characters, upper and lower case, a digit and a symbol).
2. Change it in SQL Server:

   ```bash
   docker compose exec db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -Q "ALTER LOGIN sa WITH PASSWORD = N'<new>' OLD_PASSWORD = N'<old>'"
   ```

3. Put the new password in `.env` (`SA_PASSWORD`), then `docker compose up -d` so the app reconnects with it.
4. Check `/health/ready` returns 200.

## A database login for the app

Run once as `sa`, then use `User ID=votingapp;Password=<its password>` in `ConnectionStrings__DefaultConnection` instead of `sa`. The app needs to own its database because it applies migrations on start-up.

```sql
CREATE LOGIN votingapp WITH PASSWORD = N'<password>', CHECK_POLICY = ON;
USE VotingApp;
CREATE USER votingapp FOR LOGIN votingapp;
ALTER ROLE db_owner ADD MEMBER votingapp;
```

Keep `sa` for backups and maintenance only.
