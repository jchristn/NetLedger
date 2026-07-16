@echo off
setlocal enabledelayedexpansion
pushd "%~dp0\.."

echo This will reset the NetLedger Docker deployment to factory defaults.
echo Docker containers, PostgreSQL data, stale local SQLite data, and dashboard runtime data for this compose project will be removed.
echo The docker\server\netledger.json settings file will be overwritten from factory defaults.
set "CONFIRM="
set /P "CONFIRM=Type RESET to continue: "
if not "%CONFIRM%"=="RESET" (
  echo Reset cancelled.
  popd
  endlocal
  exit /b 1
)

docker compose -f compose.yaml down --volumes --remove-orphans
if errorlevel 1 (
  popd
  endlocal
  exit /b 1
)

del /Q server\netledger.db server\netledger.db-shm server\netledger.db-wal 2>NUL
for %%F in (dashboard\*) do (
  if /I not "%%~nxF"==".gitkeep" del /F /Q "%%F" 2>NUL
)
for /D %%D in (dashboard\*) do rmdir /S /Q "%%D" 2>NUL
copy /Y factory\netledger.json server\netledger.json >NUL

docker compose -f compose.yaml up -d --build
popd
endlocal
