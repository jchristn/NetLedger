#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")"

if [ "$#" -lt 1 ] || [ -z "$1" ]; then
  echo "Usage: build-all.sh <docker-image-tag>"
  echo "Example: build-all.sh v4.0.0"
  exit 1
fi

IMAGE_TAG="$1"
./build-server.sh "$IMAGE_TAG"
./build-archive.sh "$IMAGE_TAG"
./build-dashboard.sh "$IMAGE_TAG"

echo
echo "============================================"
echo "NetLedger Docker build-all completed successfully!"
echo
echo "Components built and pushed:"
echo "  - NetLedger Server: jchristn77/netledger:$IMAGE_TAG"
echo "  - NetLedger Server: jchristn77/netledger:latest"
echo "  - NetLedger Archive Server: jchristn77/netledger-archive:$IMAGE_TAG"
echo "  - NetLedger Archive Server: jchristn77/netledger-archive:latest"
echo "  - NetLedger Dashboard: jchristn77/netledger-ui:$IMAGE_TAG"
echo "  - NetLedger Dashboard: jchristn77/netledger-ui:latest"
echo "============================================"
