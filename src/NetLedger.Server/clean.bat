@echo off
echo Cleaning NetLedger.Server data files...

if exist netledger.json (
    del /f netledger.json
    echo Deleted netledger.json
)

if exist netledger.db (
    del /f netledger.db
    echo Deleted netledger.db
)

if exist logs (
    rmdir /s /q logs
    echo Deleted logs directory
)

echo Clean complete.
