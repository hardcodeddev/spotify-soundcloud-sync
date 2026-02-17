# Spotify ↔ SoundCloud Sync

A local development setup for syncing playlists between Spotify and SoundCloud.

## First-time setup

### 1) Prerequisites

Make sure you have:

- [Docker](https://www.docker.com/) + Docker Compose
- Git
- A Spotify developer app
- A SoundCloud developer app
- An HTTPS tunnel tool (for OAuth callbacks), for example:
  - [ngrok](https://ngrok.com/)
  - [Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/)

## 2) Clone and open the project

```bash
git clone <your-repo-url>
cd spotify-soundcloud-sync
```

## 3) Start the stack

From the repository root:

```bash
docker compose up --build
```

This starts:

- API (local): `http://localhost:5000`
- Web UI: `http://localhost:5173`
- Postgres: `localhost:5432`

The API applies database migrations automatically on startup.

## 4) Create a secure callback URL (required)

Spotify and SoundCloud require **HTTPS callback URLs**. Local `http://localhost:5000/...` callback URLs will not work for OAuth app registration.

Expose the local API through an HTTPS tunnel that forwards to port `5000`, for example:

```bash
# Example with ngrok
ngrok http 5000
```

Copy the generated HTTPS base URL (example: `https://abc123.ngrok-free.app`).

## 5) Configure OAuth credentials

The API reads OAuth settings from:

- `src/backend/PlaylistSync.Api/appsettings.Development.json`

Edit that file and replace placeholder values:

- `OAuth.Spotify.ClientId`
- `OAuth.Spotify.ClientSecret`
- `OAuth.SoundCloud.ClientId`
- `OAuth.SoundCloud.ClientSecret`

Set callback URLs to your HTTPS tunnel domain:

- Spotify: `https://<your-tunnel-domain>/auth/spotify/callback`
- SoundCloud: `https://<your-tunnel-domain>/auth/soundcloud/callback`

> Register the exact same HTTPS callback URLs in Spotify and SoundCloud developer portals.

## 6) Verify services are running

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

## 7) First-time usage flow in the UI

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
  - Confirm callback URLs in provider dashboards exactly match your HTTPS tunnel callback URLs.
  - Ensure your tunnel is still running and points to local port `5000`.
- **API fails with `Failed to connect to 127.0.0.1:5432`**
  - This project is configured so the API connects to Postgres using Docker service DNS (`db`), not `localhost`, when running in Compose.
  - If you changed connection settings, set DB host back to `db` for container-to-container access.
  - Recreate services after config changes: `docker compose down && docker compose up --build`.
- **Web UI cannot reach API**
  - Confirm API is healthy at `http://localhost:5000/health`.
  - Confirm `docker compose` is running and ports `5000`/`5173` are free.
- **API fails with `relation "SyncProfiles" does not exist`**
  - Your local Postgres volume likely has stale/partial schema metadata from an earlier run.
  - Reset local DB volume and re-run migrations: `docker compose down -v && docker compose up --build`.

- **Need a clean database**
  - Run `docker compose down -v` and start again.
