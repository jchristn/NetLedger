#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")"

echo "============================================"
echo "NetLedger Dashboard Docker Build Script"
echo "============================================"
echo

if [ "$#" -lt 1 ] || [ -z "$1" ]; then
    echo "ERROR: Image tag is required"
    echo
    echo "Usage: build-dashboard.sh <tag>"
    echo "Example: build-dashboard.sh v4.0.0"
    exit 1
fi

IMAGE_NAME=jchristn77/netledger-ui
IMAGE_TAG="$1"
DOCKERFILE_PATH=src/NetLedger.Dashboard/Dockerfile
PLATFORMS=linux/amd64,linux/arm64/v8

echo "Image: $IMAGE_NAME:$IMAGE_TAG"
echo "Platforms: $PLATFORMS"
echo

if ! docker --version >/dev/null 2>&1; then
    echo "ERROR: Docker is not installed or not in PATH"
    exit 1
fi

if ! docker buildx version >/dev/null 2>&1; then
    echo "ERROR: Docker buildx is not available"
    echo "Please ensure Docker Desktop is installed with buildx support"
    exit 1
fi

echo "Creating/using buildx builder..."
docker buildx create --name netledger-builder --use 2>/dev/null || docker buildx use netledger-builder

docker buildx inspect --bootstrap

echo
echo "Building and pushing multi-platform image..."
echo

if ! docker buildx build \
    --platform "$PLATFORMS" \
    --tag "$IMAGE_NAME:$IMAGE_TAG" \
    --tag "$IMAGE_NAME:latest" \
    --file "$DOCKERFILE_PATH" \
    --push \
    .; then
    echo
    echo "ERROR: Build failed!"
    exit 1
fi

echo
echo "============================================"
echo "Build and push completed successfully!"
echo
echo "Images pushed:"
echo "  - $IMAGE_NAME:$IMAGE_TAG"
echo "  - $IMAGE_NAME:latest"
echo
echo "Platforms: $PLATFORMS"
echo "============================================"
