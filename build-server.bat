@echo off
setlocal enabledelayedexpansion

echo ============================================
echo NetLedger Server Docker Build Script
echo ============================================
echo.

REM Check if image tag argument is provided
if "%~1"=="" (
    echo ERROR: Image tag is required
    echo.
    echo Usage: build-server.bat ^<tag^>
    echo Example: build-server.bat v2.0.1
    exit /b 1
)

set IMAGE_NAME=jchristn/netledger
set IMAGE_TAG=%~1
set DOCKERFILE_PATH=src/NetLedger.Server/Dockerfile
set PLATFORMS=linux/amd64,linux/arm64/v8

echo Image: %IMAGE_NAME%:%IMAGE_TAG%
echo Platforms: %PLATFORMS%
echo.

REM Check if docker is available
docker --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: Docker is not installed or not in PATH
    exit /b 1
)

REM Check if buildx is available
docker buildx version >nul 2>&1
if errorlevel 1 (
    echo ERROR: Docker buildx is not available
    echo Please ensure Docker Desktop is installed with buildx support
    exit /b 1
)

REM Create or use existing buildx builder
echo Creating/using buildx builder...
docker buildx create --name netledger-builder --use 2>nul || docker buildx use netledger-builder

REM Ensure builder is running
docker buildx inspect --bootstrap

echo.
echo Building and pushing multi-platform image...
echo.

docker buildx build ^
    --platform %PLATFORMS% ^
    --tag %IMAGE_NAME%:%IMAGE_TAG% ^
    --tag %IMAGE_NAME%:latest ^
    --file %DOCKERFILE_PATH% ^
    --push ^
    .

if errorlevel 1 (
    echo.
    echo ERROR: Build failed!
    exit /b 1
)

echo.
echo ============================================
echo Build and push completed successfully!
echo.
echo Images pushed:
echo   - %IMAGE_NAME%:%IMAGE_TAG%
echo   - %IMAGE_NAME%:latest
echo.
echo Platforms: %PLATFORMS%
echo ============================================

endlocal
