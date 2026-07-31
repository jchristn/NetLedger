#!/usr/bin/env sh
set -eu

server_url="${NETLEDGER_SERVER_URL:-}"
archive_server_url="${NETLEDGER_ARCHIVE_SERVER_URL:-}"
escaped_server_url="$(printf '%s' "$server_url" | sed 's/\\/\\\\/g; s/"/\\"/g')"
escaped_archive_server_url="$(printf '%s' "$archive_server_url" | sed 's/\\/\\\\/g; s/"/\\"/g')"

cat > /usr/share/nginx/html/config.js <<EOF
window.NETLEDGER_CONFIG = window.NETLEDGER_CONFIG || {};
window.NETLEDGER_CONFIG.serverUrl = "$escaped_server_url";
window.NETLEDGER_CONFIG.archiveServerUrl = "$escaped_archive_server_url";
EOF

exec nginx -g 'daemon off;'
