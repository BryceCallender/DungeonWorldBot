# DungeonWorldBot

A Discord bot and web dashboard for running our Dungeon World campaign, **Sana Solem**. The bot handles everything at
the table through slash commands — character sheets, moves and rolls, gear, spells, fights and sessions — and the
dashboard turns the same data into a site the party can browse between (and during) game nights.

## What's in it

### The bot

- **Characters**: guided creation (`/character create`), sheets, bonds, HP and damage dice, leveling and stat points,
  debilities, armor, inventory and coins.
- **Classes**: the core Dungeon World classes, plus GrimWorld (Battlemaster, Channeler, Necromancer, Shaman, Slayer,
  Templar) and Inverse World (Captain, Collector, Golem, Lantern, Mechanic, Sky Dancer, Survivor, Walker) classes,
  with multiclassing, homebrew subclasses and per-class guides.
- **Moves and rolls**: basic, special, class and downtime moves resolve against the character's stats; plain dice
  rolls, dramatic rolls, and a "Change outcome" message command for corrections.
- **Class mechanics**: spells, shapeshifting, techniques, tactics, brands, light, spirits, hexed jars, quests and
  vows, and tracked class resources.
- **Fights**: DM-run encounters with monsters from a saved bestiary, spotlight tracking, enemy attacks, NPCs, and
  move requests that the bot announces in game chat.
- **Sessions**: start and end game nights, and roll stats gathered from the game chat channel.

### The dashboard

A Blazor site that reads the bot's database live. It has the party and each character's sheet, story and timeline,
classes, moves and spells, NPCs and their relationship web, the bestiary, past sessions with fight replays, roll
stats and awards, and shared scene canvases (Konva + SignalR) the DM can run fights on. Admins also get a database
editor and a Health page.

## Project layout

| Path | What it is |
| --- | --- |
| `src/DungeonWorldBot` | The Discord bot ([Remora.Discord](https://github.com/Remora/Remora.Discord)): its commands, autocomplete, interactions and background workers, under `Features/`. |
| `src/DungeonWorldBot.Core` | The game's rules (`Rules/`, class sheets, subclasses) and the services that keep track of it, shared by the bot and dashboard. Same namespaces and folders as the bot, with nothing of Discord's commands or gateway. |
| `src/DungeonWorldBot.Data` | EF Core entities, the SQLite contexts and migrations, shared by the bot and dashboard. |
| `src/DungeonWorldBot.Dashboard` | The ASP.NET Core / Blazor dashboard. Canvas scripts are TypeScript in `Scripts/`. |
| `tests/DungeonWorldBot.Tests` | xUnit tests for the bot's features and services. |
| `scripts/seed.cs` | Builds a made-up test campaign to try things against (see below). |
| `scripts/pi/tunnel.sh` | Shares the dashboard from the Raspberry Pi through Tailscale Funnel. |

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org) with npm, for the dashboard's canvas scripts (built automatically on `dotnet build`)
- A Discord application with a bot token, invited to your server with the `applications.commands` scope. The bot uses
  the **Message Content** intent, so turn that on in the developer portal.

## Running the bot

The bot reads its settings from `appsettings.json` (not committed), user secrets, or environment variables of the
same names:

| Setting | Required | What it does |
| --- | --- | --- |
| `REMORA_BOT_TOKEN` | Yes | The Discord bot token. The bot won't start without it. |
| `ConnectionStrings:DungeonWorldConnectionString` | Yes | SQLite connection string for the campaign database, e.g. `Data Source=SanaSolem.db`. |
| `GameChatChannelId` | | The channel whose rolls count toward roll stats and where fight announcements go. |
| `DashboardUrl` | | The dashboard's public address, for links the bot posts. |
| `DungeonMasterRoleName` | | The server role that makes someone the DM. Defaults to `DM`. |
| `CommandServerId` | | The server to register slash commands on, so changes show up there right away. Left out, they're registered globally. |
| `GameSettingsPath` | | Where the DM's game settings are saved. Defaults to `game-settings.json`. |
| `CanvasDatabasePath` | | The dashboard's `canvases.db`, which the bot reads for encounter scenes. Defaults to beside the dashboard. |

```bash
dotnet user-secrets set REMORA_BOT_TOKEN "<token>" --project src/DungeonWorldBot
```

```bash
dotnet run --project src/DungeonWorldBot
```

Slash commands are registered with Discord on startup.

### Database migrations

Migrations live in `src/DungeonWorldBot.Data/Migrations`. To add one and apply it:

```bash
dotnet ef migrations add <Name> --project src/DungeonWorldBot.Data --startup-project src/DungeonWorldBot
```

```bash
dotnet ef database update --project src/DungeonWorldBot.Data --startup-project src/DungeonWorldBot
```

## Running the dashboard

```bash
dotnet run --project src/DungeonWorldBot.Dashboard
```

It serves on http://localhost:5280 and reads the bot's `src/DungeonWorldBot/SanaSolem.db` by default. Settings:

| Setting | What it does |
| --- | --- |
| `DatabasePath` | The bot's database, if not at the default path. |
| `CanvasDatabasePath` | The dashboard's own scene database. Defaults to `canvases.db` beside the dashboard. |
| `DungeonMasterId` | The DM's Discord user ID: whose rolls are the DM's and who can do DM things on the site. |
| `TrackWeight` | `true` if the table tracks item weight and load. Off by default. |
| `Discord:ClientId`, `Discord:ClientSecret` | Turn on "Sign in with Discord". Without them the dashboard is open to anyone who can reach it. |
| `Discord:GuildId` | Limits sign-in to members of this server. |
| `Discord:AdminRoleId` | The server role that can use the admin pages. |
| `Discord:SignIn` | Set to `false` to skip sign-in while testing locally. |
| `TelemetryDashboardUrl` | Links the Health page to a telemetry dashboard (see below). |

Keep the Discord client secret in user secrets:

```bash
dotnet user-secrets set Discord:ClientSecret "<secret>" --project src/DungeonWorldBot.Dashboard
```

For sign-in, add `https://<your-address>/signin-discord` as a redirect in the Discord developer portal.

### Sharing it from the Pi

The bot and dashboard run on a Raspberry Pi. To make the dashboard reachable by the party, start it there and run:

```bash
./scripts/pi/tunnel.sh
```

This sets up a free [Tailscale Funnel](https://tailscale.com/kb/1223/funnel) with a fixed `https://<pi>.<tailnet>.ts.net`
address (friends don't need Tailscale) and prints the redirect to add for Discord sign-in. It keeps running through
reboots; `./scripts/pi/tunnel.sh off` stops it. `DASHBOARD_PORT` changes the port it shares (default 5280).

## Trying it out with test data

`scripts/seed.cs` builds a fresh, made-up campaign — a full party, NPCs, a bestiary, ten past sessions with fights,
and a fight still running — into separate test databases, so the real ones are never touched:

```bash
dotnet run scripts/seed.cs
```

```bash
dotnet run --project src/DungeonWorldBot.Dashboard -- --DatabasePath=../DungeonWorldBot/SanaSolem.test.db --CanvasDatabasePath=../DungeonWorldBot/canvases.test.db --Discord:SignIn=false
```

## Tests

```bash
dotnet test
```

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

## License

[MIT](LICENSE)
