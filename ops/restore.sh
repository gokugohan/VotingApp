#!/usr/bin/env bash
# Restores a backup made by ops/backup.sh.
#
#   ops/restore.sh --drill backups/20271101T120000Z   # restore into a scratch database, compare, drop it
#   ops/restore.sh backups/20271101T120000Z           # REPLACE the live database and files
#
# A drill does not touch the live data; run one after every backup you rely on. A real restore stops
# the app, replaces the database and the files, and starts the app again.
# Environment: ENV_FILE (default .env), COMPOSE_PROJECT_NAME (optional), DB_NAME (default VotingApp).
set -euo pipefail

drill=false
if [ "${1:-}" = "--drill" ]; then drill=true; shift; fi
src="${1:?Usage: ops/restore.sh [--drill] <backup folder>}"
ENV_FILE="${ENV_FILE:-.env}"
DB_NAME="${DB_NAME:-VotingApp}"

[ -f "$src/$DB_NAME.bak" ] || { echo "No $src/$DB_NAME.bak" >&2; exit 1; }
(cd "$src" && sha256sum -c --quiet SHA256SUMS) || { echo "Checksums do not match: the backup is damaged." >&2; exit 1; }
SA_PASSWORD="$(grep -E '^SA_PASSWORD=' "$ENV_FILE" | head -1 | cut -d= -f2-)"

dc() { docker compose --env-file "$ENV_FILE" "$@"; }
sql() { dc exec -T -e SQLCMDPASSWORD="$SA_PASSWORD" db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b -h -1 -W -Q "SET NOCOUNT ON; $1"; }

target="$DB_NAME"
$drill && target="${DB_NAME}_restore_drill"
bak="/var/opt/mssql/backup/restore-$(date -u +%s).bak"
dc exec -T db mkdir -p /var/opt/mssql/backup
dc cp "$src/$DB_NAME.bak" "db:$bak"

# Data and log files get names of their own, so a drill never collides with the live database.
moves="$(sql "DECLARE @f TABLE (LogicalName nvarchar(128), PhysicalName nvarchar(260), Type char(1), FileGroupName nvarchar(128), Size numeric(20,0), MaxSize numeric(20,0), FileId bigint, CreateLSN numeric(25,0), DropLSN numeric(25,0), UniqueId uniqueidentifier, ReadOnlyLSN numeric(25,0), ReadWriteLSN numeric(25,0), BackupSizeInBytes bigint, SourceBlockSize int, FileGroupId int, LogGroupGUID uniqueidentifier, DifferentialBaseLSN numeric(25,0), DifferentialBaseGUID uniqueidentifier, IsReadOnly bit, IsPresent bit, TDEThumbprint varbinary(32), SnapshotUrl nvarchar(360));
INSERT @f EXEC('RESTORE FILELISTONLY FROM DISK = N''$bak''');
SELECT STRING_AGG('MOVE N''' + LogicalName + ''' TO N''/var/opt/mssql/data/$target' + CASE Type WHEN 'L' THEN '_log.ldf' ELSE '.mdf' END + '''', ', ') FROM @f;")"

if ! $drill; then
    echo "Stopping the app and replacing $DB_NAME."
    dc stop app
    sql "IF DB_ID(N'$DB_NAME') IS NOT NULL ALTER DATABASE [$DB_NAME] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;"
fi
sql "RESTORE DATABASE [$target] FROM DISK = N'$bak' WITH $moves, REPLACE, CHECKSUM, RECOVERY;"
dc exec -T db rm -f "$bak"
$drill || sql "ALTER DATABASE [$DB_NAME] SET MULTI_USER;"

sql "SELECT 'elections', COUNT(*) FROM [$target].dbo.Elections UNION ALL SELECT 'result_sheets', COUNT(*) FROM [$target].dbo.ResultSheets UNION ALL SELECT 'audit_entries', COUNT(*) FROM [$target].dbo.AuditLogs" \
    | tr -s ' ' > /tmp/votingapp-restored-counts.txt
if diff -q "$src/counts.txt" /tmp/votingapp-restored-counts.txt > /dev/null; then
    echo "Database restored as $target; counts match the backup:"
else
    echo "Database restored as $target, but the counts differ from the backup:" >&2
    diff "$src/counts.txt" /tmp/votingapp-restored-counts.txt >&2 || true
    $drill && exit 1
fi
cat /tmp/votingapp-restored-counts.txt

if $drill; then
    if ! listing="$(tar -tzf - < "$src/files.tar.gz")"; then
        echo "The files archive cannot be read." >&2
        sql "DROP DATABASE [$target];"
        exit 1
    fi
    echo "Files archive reads cleanly ($(printf '%s\n' "$listing" | grep -vc '/$') files)."
    sql "DROP DATABASE [$target];"
    echo "Drill passed; scratch database dropped."
else
    echo "Restoring files."
    dc run --rm --no-deps -T --entrypoint tar app xzf - -C / < "$src/files.tar.gz"
    dc start app
    echo "Restore complete. Check /health/ready and the audit log."
fi
