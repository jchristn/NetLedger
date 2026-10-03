#!/usr/bin/env sh
set -eu

server_url="${NETLEDGER_SERVER_URL:-}"
archive_server_url="${NETLEDGER_ARCHIVE_SERVER_URL:-}"
escaped_server_url="$(printf '%s' "$server_url" | sed 's/\\/\\\\/g; s/"/\\"/g')"
escaped_archive_server_url="$(printf '%s' "$archive_server_url" | sed 's/\\/\\\\/g; s/"/\\"/g')"

# Optional External Services card overrides. Unset keeps the built-in default; an empty URL hides the service.
escape() {
  printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'
}

service_entry() {
  key="$1"; url_var="$2"; user_var="$3"; pass_var="$4"; api_key_var="${5:-}"
  entry=""
  eval "url_set=\${$url_var+x}"
  if [ -n "$url_set" ]; then eval "value=\${$url_var}"; entry="${entry}\"url\": \"$(escape "$value")\","; fi
  if [ -n "$user_var" ]; then eval "user_set=\${$user_var+x}"; if [ -n "$user_set" ]; then eval "value=\${$user_var}"; entry="${entry}\"username\": \"$(escape "$value")\","; fi; fi
  if [ -n "$pass_var" ]; then eval "pass_set=\${$pass_var+x}"; if [ -n "$pass_set" ]; then eval "value=\${$pass_var}"; entry="${entry}\"password\": \"$(escape "$value")\","; fi; fi
  if [ -n "$api_key_var" ]; then eval "api_key_set=\${$api_key_var+x}"; if [ -n "$api_key_set" ]; then eval "value=\${$api_key_var}"; entry="${entry}\"apiKey\": \"$(escape "$value")\","; fi; fi
  if [ -n "$entry" ]; then printf '"%s": { %s },' "$key" "${entry%,}"; fi
}

external_services="$(service_entry grafana NETLEDGER_GRAFANA_URL NETLEDGER_GRAFANA_USERNAME NETLEDGER_GRAFANA_PASSWORD)"
external_services="${external_services}$(service_entry prometheus NETLEDGER_PROMETHEUS_URL "" "")"
external_services="${external_services}$(service_entry tempo NETLEDGER_TEMPO_URL "" "")"
external_services="${external_services}$(service_entry loki NETLEDGER_LOKI_URL "" "")"
external_services="${external_services}$(service_entry less3 NETLEDGER_LESS3_URL NETLEDGER_LESS3_ACCESS_KEY NETLEDGER_LESS3_SECRET_KEY NETLEDGER_LESS3_ADMIN_API_KEY)"
external_services="{ ${external_services%,} }"

cat > /usr/share/nginx/html/config.js <<EOF
window.NETLEDGER_CONFIG = window.NETLEDGER_CONFIG || {};
window.NETLEDGER_CONFIG.serverUrl = "$escaped_server_url";
window.NETLEDGER_CONFIG.archiveServerUrl = "$escaped_archive_server_url";
window.NETLEDGER_CONFIG.externalServices = $external_services;
EOF

exec nginx -g 'daemon off;'
