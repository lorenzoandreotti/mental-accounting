#!/bin/sh
set -eu
if [ "$#" -ne 2 ] || [ "$2" != "--confirm-replace" ]; then
  echo "Uso: restore.sh /percorso/backup.dump --confirm-replace (sostituisce il database)" >&2
  exit 2
fi
test -r "$1"
dump=$(realpath -- "$1")
base=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
docker compose --project-directory "$base" -f "$base/compose.yaml" stop web
docker compose --project-directory "$base" -f "$base/compose.yaml" exec -T db pg_restore -U mental_accounting -d mental_accounting --clean --if-exists --no-owner --single-transaction < "$dump"
docker compose --project-directory "$base" -f "$base/compose.yaml" up -d web
echo "Ripristino completato."
