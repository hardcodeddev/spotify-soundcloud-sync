# Spotify ↔ SoundCloud Sync

A local development setup for syncing playlists between Spotify and SoundCloud.

## First-time setup

### 1) Prerequisites

Make sure you have:

- [Docker](https://www.docker.com/) + Docker Compose
- Git
- A Spotify developer app
- A SoundCloud developer app

## 2) Clone and open the project

```bash
git clone <your-repo-url>
cd spotify-soundcloud-sync
```

## 3) Configure OAuth credentials

The API reads OAuth settings from `src/backend/PlaylistSync.Api/appsettings.Development.json`.

Edit that file and replace the placeholder values:

- `OAuth.Spotify.ClientId`
- `OAuth.Spotify.ClientSecret`
- `OAuth.SoundCloud.ClientId`
- `OAuth.SoundCloud.ClientSecret`

Keep callback URLs set to:

- Spotify: `http://localhost:5000/auth/spotify/callback`
- SoundCloud: `http://localhost:5000/auth/soundcloud/callback`

> You must also register those same callback URLs in the Spotify and SoundCloud developer portals.

## 4) Start the stack

From the repository root:

```bash
docker compose up --build
```

This starts:

- API: `http://localhost:5000`
- Web UI: `http://localhost:5173`
- Postgres: `localhost:5432`

The API applies database migrations automatically on startup.

## 5) Verify services are running

In a new terminal:

```bash
curl http://localhost:5000/health
```

Expected response:

```json
{"status":"ok"}
```

Then open the web app:

- `http://localhost:5173`

## 6) First-time usage flow in the UI

1. Go to **Connections**.
2. Click **Connect Spotify** and complete auth.
3. Click **Connect SoundCloud** and complete auth.
4. Go to **Sync**.
5. Configure:
   - sync direction
   - likes sync behavior
   - cron schedule (for example `*/30 * * * *`)
   - playlist mappings (source/target providers + playlist IDs)
6. Save sync configuration.
7. Optionally run a manual sync from the **Run now** panel.

## Common commands

```bash
# Start in foreground
docker compose up --build

# Start in background
docker compose up -d --build

# Stop containers
docker compose down

# Stop containers and remove DB volume (reset local data)
docker compose down -v
```

## Troubleshooting

- **OAuth fails or popup closes immediately**
  - Re-check client IDs/secrets in `appsettings.Development.json`.
  - Confirm callback URLs in provider dashboards exactly match the local callback URLs.
- **Web UI cannot reach API**
  - Confirm API is healthy at `http://localhost:5000/health`.
  - Confirm `docker compose` is running and ports 5000/5173 are free.
- **Need a clean database**
  - Run `docker compose down -v` and start again.
