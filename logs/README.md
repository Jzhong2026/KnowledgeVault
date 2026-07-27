# Centralized log directory

All local logs and one-off debug output are written here:

- `backend/runtime/` — Serilog JSON-line files produced by the KnowledgeVault
  API at runtime (`knowledge-vault-YYYYMMDD.jsonl`, 30-day rolling retention).
- `backend/dev/` — Output captured by `dotnet run` / `dotnet watch` during
  local development (stdout/stderr of the API process).
- `frontend/` — Output captured by the Angular dev server, SSR, and any
  client-side debug logs.
- `build/` — `dotnet publish` and `ng build` outputs.
- `migrations/` — `dotnet ef database update` and migration script dumps.
- `tools/` — Misc tooling logs (EF install/version probes, MCP smoke runs).
- `scratch/` — Throwaway debug dumps (e2e, register, npm errors).

This directory is `.gitignore`d; the `.keep` files preserve the empty
subdirectory layout so new clones still get the structure on first run.

## Configuring the location

The backend reads `Logs:Directory` from configuration. Defaults:

| Environment | Value                        |
|-------------|------------------------------|
| Development | `logs/backend/runtime` (relative to ContentRootPath) |
| Production  | `/app/logs` (mounted as the `kv-logs` Docker volume) |

The `logs` volume in `docker-compose.yml` keeps the files outside the
container's writable layer.
