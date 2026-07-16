@echo off
setlocal
if "%~1"=="" (
  echo Usage: build-all.bat ^<docker-image-tag^>
  exit /b 1
)

set "IMAGE_TAG=%~1"
call build-server.bat "%IMAGE_TAG%" || exit /b 1
call build-dashboard.bat "%IMAGE_TAG%" || exit /b 1
endlocal
