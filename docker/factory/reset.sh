#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")/.."

echo "This will reset the NetLedger Docker deployment to factory defaults."
echo "Docker containers, PostgreSQL data, stale local SQLite data, archive catalog data, Less3 object data, and dashboard runtime data for this compose project will be removed."
echo "The docker/server, docker/archive-server, and docker/less3 settings files will be overwritten from factory defaults."
printf "Type RESET to continue: "
IFS= read -r CONFIRM
if [ "$CONFIRM" != "RESET" ]; then
  echo "Reset cancelled."
  exit 1
fi

docker compose -f compose.yaml down --volumes --remove-orphans
mkdir -p archive-server less3/db less3/disk less3/logs less3/temp
rm -f server/netledger.db server/netledger.db-shm server/netledger.db-wal
rm -f archive-server/netledger.archive.catalog.db archive-server/netledger.archive.catalog.db-shm archive-server/netledger.archive.catalog.db-wal
rm -f less3/db/less3.db less3/db/less3.db-shm less3/db/less3.db-wal
find dashboard -mindepth 1 ! -name .gitkeep -exec rm -rf {} +
find less3/disk -mindepth 1 ! -name .gitkeep -exec rm -rf {} +
find less3/temp -mindepth 1 ! -name .gitkeep -exec rm -rf {} +
find less3/logs -mindepth 1 ! -name .gitkeep -exec rm -rf {} +
cp factory/netledger.json server/netledger.json
cp factory/netledger.archive.json archive-server/netledger.json
cp factory/less3.system.json less3/system.json
docker compose -f compose.yaml up -d --build
