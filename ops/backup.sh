#!/usr/bin/env bash
# Backs up a running VotingApp stack (docker compose): the database, the photos of signed result
# sheets, candidate photos and the data-protection keys (without them every sign-in cookie is lost).
#
#   ops/backup.sh                    # writes backups/<UTC timestamp>/
#   BACKUP_DIR=/mnt/offsite ops/backup.sh
#
# Environment: ENV_FILE (default .env, must define SA_PASSWORD), COMPOSE_PROJECT_NAME (optional),
# BACKUP_DIR (default ./backups), BACKUP_KEEP (backups to keep, default 14), DB_NAME (default VotingApp).
# Run it from the folder that holds docker-compose.yml. Copy the result off the server.
set -euo pipefail

ENV_FILE="${ENV_FILE:-.env}"
BACKUP_DIR="${BACKUP_DIR:-./backups}"
BACKUP_KEEP="${BACKUP_KEEP:-14}"
DB_NAME="${DB_NAME:-VotingApp}"

[ -f "$ENV_FILE" ] || { echo "No $ENV_FILE (set ENV_FILE)." >&2; exit 1; }
SA_PASSWORD="$(grep -E '^SA_PASSWORD=' "$ENV_FILE" | head -1 | cut -d= -f2-)"
[ -n "$SA_PASSWORD" ] || { echo "SA_PASSWORD is empty in $ENV_FILE." >&2; exit 1; }

dc() { docker compose --env-file "$ENV_FILE" "$@"; }
sql() { dc exec -T -e SQLCMDPASSWORD="$SA_PASSWORD" db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b -h -1 -W -Q "SET NOCOUNT ON; $1"; }

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
out="$BACKUP_DIR/$stamp"
mkdir -p "$out"
bak="/var/opt/mssql/backup/$DB_NAME-$stamp.bak"

echo "Database $DB_NAME -> $out/$DB_NAME.bak"
dc exec -T db mkdir -p /var/opt/mssql/backup
# COPY_ONLY leaves any scheduled backup chain alone; CHECKSUM catches damaged pages while backing up.
sql "BACKUP DATABASE [$DB_NAME] TO DISK = N'$bak' WITH COPY_ONLY, CHECKSUM, INIT, NAME = N'$DB_NAME $stamp';"
sql "RESTORE VERIFYONLY FROM DISK = N'$bak' WITH CHECKSUM;"
dc cp "db:$bak" "$out/$DB_NAME.bak"
dc exec -T db rm -f "$bak"

echo "Files (sheet scans, candidate photos, keys) -> $out/files.tar.gz"
dc exec -T app tar czf - -C / app/wwwroot/uploads/sheets app/wwwroot/images/candidates keys > "$out/files.tar.gz"

# What was backed up, for the restore drill to compare against.
sql "SELECT 'elections', COUNT(*) FROM [$DB_NAME].dbo.Elections UNION ALL SELECT 'result_sheets', COUNT(*) FROM [$DB_NAME].dbo.ResultSheets UNION ALL SELECT 'audit_entries', COUNT(*) FROM [$DB_NAME].dbo.AuditLogs" \
    | tr -s ' ' > "$out/counts.txt"
(cd "$out" && sha256sum "$DB_NAME.bak" files.tar.gz counts.txt > SHA256SUMS)

echo "Done: $out"
cat "$out/counts.txt"

# Keep the newest $BACKUP_KEEP backups.
ls -1d "$BACKUP_DIR"/*/ 2>/dev/null | sort | head -n -"$BACKUP_KEEP" | xargs -r rm -rf
