#!/bin/sh
set -eu
umask 077
base=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
target="$base/backups"
mkdir -p "$target"
# Lock prevents overlapping scheduled runs from deleting another run's temporary output.
exec 9>"$target/.backup.lock"
flock -n 9 || exit 0
file="$target/accounting-$(date -u +%Y%m%dT%H%M%SZ).dump"
trap 'rm -f "$file.tmp"' EXIT HUP INT TERM
docker compose --project-directory "$base" -f "$base/compose.yaml" exec -T db pg_dump -U mental_accounting -d mental_accounting -Fc > "$file.tmp"
test -s "$file.tmp"
mv "$file.tmp" "$file"
# UTC timestamps sort chronologically. Keep exactly the newest 30 successful archives.
find "$target" -maxdepth 1 -type f -name 'accounting-*.dump' -printf '%f\n' | sort -r | tail -n +31 | while IFS= read -r old; do rm -- "$target/$old"; done
printf 'Backup creato: %s\n' "$file"
