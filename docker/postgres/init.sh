#!/usr/bin/env sh
set -eu

DB_HOST="${NETLEDGER_DB_HOST:-postgres}"
DB_NAME="${NETLEDGER_DB_NAME:-netledger}"
DB_USER="${NETLEDGER_DB_USER:-netledger}"
DB_PASS="${NETLEDGER_DB_PASS:-netledger}"
SUPERUSER="${POSTGRES_SUPERUSER:-postgres}"
SUPERPASS="${POSTGRES_SUPERPASS:-postgres}"

psql_super() {
  database="$1"
  sql="$2"
  PGPASSWORD="$SUPERPASS" psql -v ON_ERROR_STOP=1 -h "$DB_HOST" -U "$SUPERUSER" -d "$database" -c "$sql"
}

role_exists() {
  PGPASSWORD="$SUPERPASS" psql -v ON_ERROR_STOP=1 -h "$DB_HOST" -U "$SUPERUSER" -d postgres -tAc "SELECT 1 FROM pg_roles WHERE rolname='$DB_USER'" | grep -q 1
}

database_exists() {
  PGPASSWORD="$SUPERPASS" psql -v ON_ERROR_STOP=1 -h "$DB_HOST" -U "$SUPERUSER" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$DB_NAME'" | grep -q 1
}

echo "Initializing NetLedger PostgreSQL database."

if role_exists; then
  echo "Role '$DB_USER' already exists; updating password."
  psql_super postgres "ALTER ROLE \"$DB_USER\" WITH LOGIN PASSWORD '$DB_PASS';"
else
  echo "Creating role '$DB_USER'."
  psql_super postgres "CREATE ROLE \"$DB_USER\" WITH LOGIN PASSWORD '$DB_PASS';"
fi

if database_exists; then
  echo "Database '$DB_NAME' already exists; verifying ownership and grants."
  psql_super postgres "ALTER DATABASE \"$DB_NAME\" OWNER TO \"$DB_USER\";"
else
  echo "Creating database '$DB_NAME'."
  psql_super postgres "CREATE DATABASE \"$DB_NAME\" OWNER \"$DB_USER\";"
fi

psql_super postgres "REVOKE CONNECT ON DATABASE \"$DB_NAME\" FROM PUBLIC; GRANT CONNECT, TEMPORARY ON DATABASE \"$DB_NAME\" TO \"$DB_USER\";"
psql_super "$DB_NAME" "ALTER SCHEMA public OWNER TO \"$DB_USER\"; GRANT ALL ON SCHEMA public TO \"$DB_USER\";"

echo "Verifying NetLedger PostgreSQL login."
PGPASSWORD="$DB_PASS" psql -v ON_ERROR_STOP=1 -h "$DB_HOST" -U "$DB_USER" -d "$DB_NAME" -tAc "SELECT current_database(), current_user;" >/dev/null

echo "NetLedger PostgreSQL initialization complete."
