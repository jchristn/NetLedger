#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")/.."

echo "This will reset the NetLedger Docker deployment to factory defaults."
echo "Docker containers, PostgreSQL data, stale local SQLite data, and dashboard runtime data for this compose project will be removed."
echo "The docker/server/netledger.json settings file will be overwritten from factory defaults."
printf "Type RESET to continue: "
IFS= read -r CONFIRM
if [ "$CONFIRM" != "RESET" ]; then
  echo "Reset cancelled."
  exit 1
fi

docker compose -f compose.yaml down --volumes --remove-orphans
rm -f server/netledger.db server/netledger.db-shm server/netledger.db-wal
find dashboard -mindepth 1 ! -name .gitkeep -exec rm -rf {} +
cp factory/netledger.json server/netledger.json
docker compose -f compose.yaml up -d --build
