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
- OAuth endpoints are local mock flows used to support UI connection state:
  - `/auth/spotify/start`
  - `/auth/soundcloud/start`
- State is held in memory and resets when the API container restarts.

## Health check

```bash
curl http://localhost:5000/health
```

Expected response:

```json
{"status":"ok"}
```
