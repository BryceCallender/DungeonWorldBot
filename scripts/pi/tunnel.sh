#!/usr/bin/env bash
# Shares the dashboard with friends anywhere through a Cloudflare quick tunnel. Run it on the Pi while the dashboard
# runs there (dotnet run --project src/DungeonWorldBot.Dashboard). Stop it with Ctrl+C.
#
#   ./scripts/pi/tunnel.sh
#
# No Cloudflare account or domain needed. Cloudflare gives you a random https://....trycloudflare.com address, which
# changes every time the tunnel starts.
set -euo pipefail

port="${DASHBOARD_PORT:-5280}"

if ! command -v cloudflared >/dev/null; then
    echo "Installing cloudflared..."
    sudo mkdir -p --mode=0755 /usr/share/keyrings
    curl -fsSL https://pkg.cloudflare.com/cloudflare-main.gpg | sudo tee /usr/share/keyrings/cloudflare-main.gpg >/dev/null
    echo "deb [signed-by=/usr/share/keyrings/cloudflare-main.gpg] https://pkg.cloudflare.com/cloudflared any main" \
        | sudo tee /etc/apt/sources.list.d/cloudflared.list >/dev/null
    sudo apt-get update
    sudo apt-get install -y cloudflared
fi

if ! ss -ltn "sport = :$port" | grep -q LISTEN; then
    echo "Nothing is listening on port $port yet. Start the dashboard too, or friends will get an error." >&2
fi

# cloudflared buries the address in its log, so pull it out and show it plainly
cloudflared tunnel --no-autoupdate --url "http://localhost:$port" 2>&1 | while IFS= read -r line; do
    echo "$line"
    if [[ "$line" =~ (https://[a-z0-9-]+\.trycloudflare\.com) ]]; then
        url="${BASH_REMATCH[1]}"
        cat <<EOF

==================================================================
Send your friends: $url

For Discord sign-in, add this redirect in the Discord developer portal (it changes every restart):
  $url/signin-discord
==================================================================

EOF
    fi
done
