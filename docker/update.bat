@echo off
setlocal

pushd "%~dp0" >nul
docker compose down && docker compose pull && docker compose up -d && docker ps -a
set "EXITCODE=%ERRORLEVEL%"
popd >nul

exit /b %EXITCODE%
