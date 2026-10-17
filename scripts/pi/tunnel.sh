#!/usr/bin/env bash
# Opens the Pi's dashboard at http://localhost:5280 on this computer, through SSH. Run it here, not on the Pi.
#
#   ./scripts/pi/tunnel.sh [user@pi-address]
#
# Start the dashboard on the Pi first (./scripts/pi/dashboard.sh). Stop the tunnel with Ctrl+C.
# For Discord sign-in, add http://localhost:5280/signin-discord as a redirect in the Discord developer portal.
set -euo pipefail

pi="${1:-pi@raspberrypi.local}"
port="${DASHBOARD_PORT:-5280}"

echo "Dashboard: http://localhost:$port (Ctrl+C to stop)"

# -N: only forward, no shell. Fails straight away if the port here is taken, and notices when the Pi drops off.
exec ssh -N \
    -o ExitOnForwardFailure=yes \
    -o ServerAliveInterval=30 \
    -L "$port:localhost:$port" \
    "$pi"
