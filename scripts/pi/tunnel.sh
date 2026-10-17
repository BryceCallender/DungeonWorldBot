#!/usr/bin/env bash
# Shares the dashboard with friends anywhere through Tailscale Funnel. Run it once on the Pi, while the dashboard runs
# there (dotnet run --project src/DungeonWorldBot.Dashboard).
#
#   ./scripts/pi/tunnel.sh        start sharing
#   ./scripts/pi/tunnel.sh off    stop sharing
#
# Free, with a fixed https://<pi>.<tailnet>.ts.net address. Friends don't need Tailscale. The first run asks you to
# sign in to Tailscale and turn Funnel on, each with a link. After that it keeps running in the background, through
# reboots, until you turn it off; restarting the dashboard doesn't touch it.
set -euo pipefail

port="${DASHBOARD_PORT:-5280}"

if [[ "${1:-}" == "off" ]]; then
    sudo tailscale funnel reset
    echo "Stopped sharing the dashboard."
    exit 0
fi

if ! command -v tailscale >/dev/null; then
    echo "Installing Tailscale..."
    curl -fsSL https://tailscale.com/install.sh | sh
fi

if ! tailscale status >/dev/null 2>&1; then
    echo "Open the link below to sign in to Tailscale (a free account is fine)."
    sudo tailscale up
fi

if ! ss -ltn "sport = :$port" | grep -q LISTEN; then
    echo "Nothing is listening on port $port yet. Start the dashboard too, or friends will get an error." >&2
fi

# The first time, this prints a link to turn Funnel on for your tailnet, then waits until you do
sudo tailscale funnel --bg "$port"

host="$(tailscale status --json | python3 -c 'import json, sys; print(json.load(sys.stdin)["Self"]["DNSName"].rstrip("."))')"

cat <<EOF

==================================================================
Send your friends: https://$host

For Discord sign-in, add this redirect in the Discord developer portal (once; it doesn't change):
  https://$host/signin-discord
==================================================================
EOF
