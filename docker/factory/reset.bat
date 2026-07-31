@echo off
setlocal enabledelayedexpansion
pushd "%~dp0\.."

echo This will reset the NetLedger Docker deployment to factory defaults.
echo Docker containers, PostgreSQL data, stale local SQLite data, archive catalog data, Less3 object data, and dashboard runtime data for this compose project will be removed.
echo The docker\server, docker\archive-server, and docker\less3 settings files will be overwritten from factory defaults.
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
del /Q archive-server\netledger.archive.catalog.db archive-server\netledger.archive.catalog.db-shm archive-server\netledger.archive.catalog.db-wal 2>NUL
if not exist less3 mkdir less3
if not exist less3\db mkdir less3\db
if not exist less3\disk mkdir less3\disk
if not exist less3\logs mkdir less3\logs
if not exist less3\temp mkdir less3\temp
del /Q less3\db\less3.db less3\db\less3.db-shm less3\db\less3.db-wal 2>NUL
for %%F in (dashboard\*) do (
  if /I not "%%~nxF"==".gitkeep" del /F /Q "%%F" 2>NUL
)
for /D %%D in (dashboard\*) do rmdir /S /Q "%%D" 2>NUL
for %%F in (less3\disk\*) do (
  if /I not "%%~nxF"==".gitkeep" del /F /Q "%%F" 2>NUL
)
for /D %%D in (less3\disk\*) do rmdir /S /Q "%%D" 2>NUL
for %%F in (less3\temp\*) do (
  if /I not "%%~nxF"==".gitkeep" del /F /Q "%%F" 2>NUL
)
for /D %%D in (less3\temp\*) do rmdir /S /Q "%%D" 2>NUL
for %%F in (less3\logs\*) do (
  if /I not "%%~nxF"==".gitkeep" del /F /Q "%%F" 2>NUL
)
for /D %%D in (less3\logs\*) do rmdir /S /Q "%%D" 2>NUL
if not exist archive-server mkdir archive-server
copy /Y factory\netledger.json server\netledger.json >NUL
copy /Y factory\netledger.archive.json archive-server\netledger.json >NUL
copy /Y factory\less3.system.json less3\system.json >NUL

docker compose -f compose.yaml up -d --build
popd
endlocal
