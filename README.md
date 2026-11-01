# DungeonWorldBot
D&amp;D discord bot for our campaign needs

## Keeping an eye on it

**Health page.** The dashboard's Health tab (admins only) shows whether the bot is up, what it's run since it started,
the latest errors from the bot and the dashboard, the databases on disk and the rolls of the last two weeks. The bot
writes a small `SanaSolem.status.json` beside its database every 30 seconds for it, so it works wherever the two run,
as long as the dashboard can read the bot's database.

**Logs, traces and metrics.** Both send OpenTelemetry to whatever `OTEL_EXPORTER_OTLP_ENDPOINT` names, and nothing
when it isn't set. The free [Aspire Dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone) is enough to look at them:

```bash
docker run --rm -d --name sana-solem-telemetry -p 18888:18888 -p 4317:18889 -e ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true mcr.microsoft.com/dotnet/aspire-dashboard:13.6.2
```

Then start the bot and the dashboard with `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` (use the machine's
address instead of localhost when the bot runs on the Pi) and open http://localhost:18888. You get:

- **Structured logs** from both, searchable and filterable by level.
- **Traces**: every slash command as `/roll`, `/dm npc add` and so on, with how long it took and whether it failed, each
  call to Discord's API, and each dashboard page and form post.
- **Metrics**: `sanasolem.commands`, `sanasolem.command.duration` and `sanasolem.rolls` (by 10+, 7–9 and 6−), plus
  memory, garbage collection and HTTP timings.

The Aspire Dashboard keeps everything in memory, so it starts empty each time. Set `TelemetryDashboardUrl` on the
dashboard to link the Health page to it from elsewhere. The same endpoint setting works for Grafana, Honeycomb or
Datadog's OTLP intake if that's ever wanted.
