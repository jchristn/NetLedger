#!/bin/bash
echo "Cleaning NetLedger.Server data files..."

if [ -f netledger.json ]; then
    rm -f netledger.json
    echo "Deleted netledger.json"
fi

if [ -f netledger.db ]; then
    rm -f netledger.db
    echo "Deleted netledger.db"
fi

if [ -d logs ]; then
    rm -rf logs
    echo "Deleted logs directory"
fi

echo "Clean complete."
