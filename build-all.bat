@echo off
setlocal
if "%~1"=="" (
  echo Usage: build-all.bat ^<docker-image-tag^>
  echo Example: build-all.bat v4.0.0
  exit /b 1
)

set "IMAGE_TAG=%~1"
call build-server.bat "%IMAGE_TAG%" || exit /b 1
call build-archive.bat "%IMAGE_TAG%" || exit /b 1
call build-dashboard.bat "%IMAGE_TAG%" || exit /b 1

echo.
echo ============================================
echo NetLedger Docker build-all completed successfully!
echo.
echo Components built and pushed:
echo   - NetLedger Server: jchristn77/netledger:%IMAGE_TAG%
echo   - NetLedger Server: jchristn77/netledger:latest
echo   - NetLedger Archive Server: jchristn77/netledger-archive:%IMAGE_TAG%
echo   - NetLedger Archive Server: jchristn77/netledger-archive:latest
echo   - NetLedger Dashboard: jchristn77/netledger-ui:%IMAGE_TAG%
echo   - NetLedger Dashboard: jchristn77/netledger-ui:latest
echo ============================================

endlocal
