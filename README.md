# Spotify ↔ SoundCloud Sync

A local development setup with:

- Web UI (Vite/React): `http://localhost:5173`
- API (Node.js, in-memory state): `http://localhost:5000`

## Run

1. Create your local environment file and add real OAuth values:

```bash
cp .env.example .env
```

2. Start services:

```bash
docker compose up --build
```

## Services

- **api**: lightweight Node HTTP server at `src/backend/js-api/server.js`
- **webui**: React frontend at `src/frontend/webui`

## Notes

- No database is required.
- OAuth endpoints support live Spotify/SoundCloud auth and sync:
  - `/auth/spotify/start`
  - `/auth/soundcloud/start`
- If you previously connected Spotify before scope updates, reconnect Spotify so the token includes current scopes (notably `playlist-read-collaborative`) to avoid 403 errors reading some playlists.
- For safe deployments (including a public GitHub Pages frontend), **never store OAuth client secrets in the browser**. Configure secrets only on the API server via environment variables:
  - `SPOTIFY_CLIENT_SECRET`
  - `SOUNDCLOUD_CLIENT_SECRET`
- This repo now includes `.env` / `.env.example` to centralize local credentials and callback settings for Docker runs.
- The sync mapping UI now supports creating the destination playlist when it does not already exist.
- State is held in memory and resets when the API container restarts.

## Health check

```bash
curl http://localhost:5000/health
```

Expected response:

```json
{"status":"ok"}
```
