# Spotify ↔ SoundCloud Sync

A local development setup with:

- Web UI (Vite/React): `http://localhost:5173`
- API (Node.js, in-memory state): `http://localhost:5000`

## Run

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
- For safe deployments (including a public GitHub Pages frontend), **never store OAuth client secrets in the browser**. Configure secrets only on the API server via environment variables:
  - `SPOTIFY_CLIENT_SECRET`
  - `SOUNDCLOUD_CLIENT_SECRET`
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
