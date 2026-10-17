#!/usr/bin/env bash
# Runs the dashboard on the Pi for the tunnel to share. Stop it with Ctrl+C.
#
#   ./scripts/pi/dashboard.sh
#
# It listens only on the Pi itself, so people reach it through the tunnel (./scripts/pi/tunnel.sh), not your network.
# Needs the .NET 10 SDK. Set DatabasePath if the bot's SanaSolem.db isn't next to the bot's project.
set -euo pipefail

port="${DASHBOARD_PORT:-5280}"
repo="$(cd "$(dirname "$0")/../.." && pwd)"

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"

exec dotnet run --project "$repo/src/DungeonWorldBot.Dashboard" -c Release -- --urls "http://127.0.0.1:$port"
