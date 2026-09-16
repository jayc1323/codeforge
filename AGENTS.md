# CodeForge

Online multi-language code execution platform.

## Stack
- Backend: .NET 8 ASP.NET Core (`backend/`) — Api, Core (domain), Infrastructure (execution engine), xUnit tests
- Frontend: Angular 17 + Monaco editor (`frontend/`)

## Run (dev)
```bash
export PATH=/usr/local/dotnet:$PATH   # dotnet is NOT on PATH by default
/root/codeforge/run.sh                # starts both (API 5045, UI 80); Ctrl+C stops both
/root/codeforge/stop.sh               # stops services + stray dev processes + pyright + leftover containers
# OR as always-on services (survive reboots, auto-restart):
systemctl start codeforge-api codeforge-ui
journalctl -u codeforge-api -f        # logs
```
API binds localhost:5045 (only the Angular proxy reaches it); UI binds 0.0.0.0:80 via codeforge-ui.service (moved from 4200 because user networks often block non-standard ports). run.sh also uses port 80 now.

## Production deploy (Oracle VM)
- **Migrated to Oracle VM** (150.136.57.158, 8 vCPU / 30GB RAM) — much more powerful than previous hosts.
- Database: **SQLite** (local file `/home/opc/codeforge/.secrets/codeforge.db`) — migrated from Azure SQL for simplicity. Can be moved to Oracle PostgreSQL later if needed.
- Web server: **nginx** (replaces Caddy) — serves HTTPS on 80/443, static Angular build from /var/www/codeforge, reverse-proxies /api, /hubs, /lsp to Kestrel :5045.
- SSL: **Let's Encrypt** via certbot (manually installed via pip since not in Oracle repos).

### Deployment reference
- **URL: https://coderunner.duckdns.org** (DuckDNS -> Oracle VM 150.136.57.158)
- nginx serves https on 80/443: static Angular build from /var/www/codeforge (owned by nginx user), reverse-proxies /api, /hubs, /lsp to Kestrel :5045. Config: /etc/nginx/conf.d/codeforge.conf.
- Deploy frontend updates: ./deploy/deploy-frontend.sh (ng build -> /var/www/codeforge). 
- Deploy backend updates: ./deploy/deploy-backend.sh (dotnet publish Release -> /opt/codeforge-api, installs deploy/codeforge-api.service, restarts, health-checks :5045 for 30s, rolls back on failure). Runs the PUBLISHED BINARY (not dotnet run): boots in ~2s, no build-on-boot. Unit sets ASPNETCORE_URLS=http://localhost:5045 and ASPNETCORE_ENVIRONMENT=Production.
- Rate limiting: 10 executions/min per IP, 10 auth attempts/5min per IP (.NET 8 built-in, partitioned by X-Forwarded-For from nginx).
- Firewall: firewalld active, allow 22/80/443 only. Oracle Cloud NSGs also configured for inbound 22/80/443.
- SELinux: enabled, httpd_can_network_connect=1 set for nginx -> backend proxy.
- Secrets: moved from /home/opc/.secrets/env to /etc/codeforge/env (chmod 600) so systemd can read them.
- E2E verified over public HTTPS: register -> JWT -> authed Docker execution -> history; SignalR negotiate 200; SPA fallback serves index.html for /app, /auth.

## Debugging (read this first when something breaks)

Start here — every failure mode below was hit and fixed in production:

### Where to look
```bash
journalctl -u codeforge-api -n 50 --no-pager   # backend logs (EF SQL, exceptions, startup)
journalctl -u nginx -n 50 --no-pager           # TLS issuance, proxy errors
systemctl status codeforge-api nginx           # service state + recent log lines
docker ps -a                                   # leftover containers (should be none; codeforge-* are per-run)
```

### Failure modes we've actually hit (symptom -> cause -> fix)
1. **`status=203/EXEC` on codeforge-api** — ExecStart path in the unit doesn't exist. dotnet lives at `/usr/local/dotnet/dotnet` on the droplet (manual install) but `/usr/bin/dotnet` on Oracle VM (dnf install). Fixed by using hardcoded path in service file.
2. **`ng: not found` during frontend deploy** — fresh clone has no `node_modules` (gitignored). deploy-frontend.sh now runs `npm ci` when missing. First `npm ci` takes several minutes (~300MB).
3. **Empty response / 500 on FIRST API call after idle** — SQLite doesn't have this issue (local file), but if migrated to PostgreSQL, first connection after idle may wake it up.
4. **HTTP 200 but empty/HTML body from /api POSTs** — nginx routing issue: `try_files` before `proxy_pass` rewrites /api/* to index.html. Fixed with proper location blocks. If API calls return the SPA HTML, check the nginx config uses `location /api/` etc., not bare path matchers.
5. **403 from nginx on the frontend** — nginx runs as the `nginx` user and can't read certain directories due to SELinux or permissions. Ensure files are owned by `nginx:nginx` and SELinux context is correct (`restorecon -R /var/www/codeforge`).
6. **HTTPS fails / cert errors after migration** — certbot can only get the Let's Encrypt cert once DNS points at THIS host. Check `dig +short coderunner.duckdns.org` returns the current VM's IP. certbot retries automatically; watch `journalctl -u nginx -f`.
7. **DB connection issues** — SQLite is local file-based, no network connectivity needed. If migrated to PostgreSQL, ensure firewall allows the database port and connection string is correct.
8. **SELinux blocking container access** — Podman needs `chcon -R -t container_file_t` on mounted directories. DockerRunner now sets this automatically.
9. **TypeScript "image not known" error** — The codeforge-typescript image must be built in root's Podman storage since the systemd service runs as root. User-built images (e.g., as `opc`) are invisible to root. Fix: `sudo podman build -t localhost/codeforge-typescript:latest /home/opc/codeforge/docker/typescript/`. The image name must be `localhost/codeforge-typescript:latest` with `--pull=never` to prevent Podman from trying to pull from a registry.

### Quick E2E smoke test (run after any deploy/migration)
```bash
curl -s https://coderunner.duckdns.org/api/languages          # JSON list = API+proxy OK
curl -s -o /dev/null -w "%{http_code}\n" https://coderunner.duckdns.org   # 200 = frontend OK
curl -s -X POST https://coderunner.duckdns.org/api/executions \
  -H "Content-Type: application/json" \
  -d '{"language":"python","sourceCode":"print(1)"}'          # 202 + id = execution pipeline OK
# then GET /api/executions/{id} after ~6s -> status 2, stdout "1\n"

# Test the frontend serves correctly
curl -s https://coderunner.duckdns.org/ | grep -q "CodeForge" && echo "Frontend OK"
```

## Test
```bash
cd backend && dotnet test
```

## Architecture notes
- Execution flow: POST /api/executions (202 + id) -> Channel queue -> ExecutionWorker -> IExecutionRunner -> GET /api/executions/{id}
- Real-time: SignalR hub at /hubs/executions (ExecutionHub, group per execution id). Worker publishes "status"/"output"(live stdout/stderr chunks)/"completed" via IExecutionEventPublisher; runners stream chunks through IExecutionProgress (optional param on IExecutionRunner.RunAsync). Angular ExecutionStreamService watches per submission; falls back to 500ms polling if SignalR connect fails. Dev proxy forwards /hubs with ws:true.
- Runner selected via config `Execution:Runner` = "docker" (default in appsettings) | "local". Both behind IExecutionRunner; shared ProcessRunner helper handles timeout/capped-output.
- `DockerRunner`: ONE throwaway container per execution (compile+run merged via generated `__cf_run.sh` script). Phase attribution via marker files in /work (`__cf_compile_failed`/`__cf_compile_timeout`/`__cf_run_timeout`) + GNU `timeout` per phase inside the container + wall-clock check to disambiguate exit-code-124 collisions. Outer timeout (compile+run+15s) is a safety net with `docker rm -f`. Isolation: `--network none`, 256MB mem (no swap), 1 CPU, 128 pids, read-only rootfs, tmpfs /tmp, cap-drop ALL, no-new-privileges, user 65534, workdir mounted at /work. Images mapped in DockerRunnerOptions (python:3.12-slim, gcc:13, dotnet/sdk:8.0, codeforge-typescript built from docker/typescript/Dockerfile).
- `LocalProcessRunner` (dev fallback): runs directly on host, temp dir, same timeouts.
- Timeouts: 60s compile / 10s run default (per-language overrides in registry). 64KB output cap.
- Languages in `CodeForge.Core/Languages/LanguageRegistry.cs`: python, cpp, csharp, fsharp, typescript (haskell needs ghc).
- Status enum: 0 Queued, 1 Running, 2 Completed, 3 Failed, 4 TimedOut, 5 CompileError.
- Store is in-memory; swap for a real DB later.
- Known cosmetic issue: F# in offline container prints "An issue was encountered verifying workloads" to stdout before program output.

## Progress (as of 2026-09-16)
DONE:
- [x] .NET 8 backend: Api / Core / Infrastructure layers, xUnit tests (21 passing)
- [x] SignalR live streaming: output chunks pushed as produced (verified ~1s apart over ws through the proxy), polling kept as fallback
- [x] Execution engine: channel queue, background worker, timeouts, output caps, temp-dir cleanup
- [x] Docker isolation: DockerRunner with no-network/read-only/resource-capped containers, verified (network blocked, host FS invisible, OOM kill, no leftover containers); LocalProcessRunner kept as dev fallback
- [x] Languages: python, cpp, csharp, fsharp, typescript (tsx) — versions detected at runtime via LanguageInfoService (cached, shown in UI dropdown)
- [x] Angular 17 frontend: Monaco editor, per-language samples, stdin box, status chips, stdout/stderr panels, /api proxy, light/dark theme toggle (localStorage)
- [x] End-to-end verified through the Angular proxy (4200 -> 5045) with Docker runner active
- [x] Python LSP (Pyright) IntelliSense: backend LspBridge maps /lsp/{language} WebSocket -> pyright-langserver stdio (one server process per browser session, killed on disconnect); frontend LspClient + monaco-lsp wiring (completion, hover, diagnostics markers); lazily started when Python is selected. Verified initialize handshake through the proxy.
- [x] Persistence: **SQLite** via EF Core 8 + Microsoft.EntityFrameworkCore.Sqlite (migrated from Azure SQL). CodeForgeDbContext (IdentityDbContext<ApplicationUser>) with Executions + Snippets tables; DesignTimeDbContextFactory reads ConnectionStrings__CodeForge env var or .secrets/dbconnectionstring.txt for dotnet-ef. EfExecutionStore (IDbContextFactory-based, safe for the singleton ExecutionWorker) replaces InMemoryExecutionStore when a connection string is configured; worker persists Running + final states. Secrets in /etc/codeforge/env (chmod 600), loaded by codeforge-api.service via EnvironmentFile. **Note**: Azure SQL config preserved in git history for reference.
- [x] Auth: ASP.NET Core Identity (email+password, min 8 chars) + JWT bearer (HMAC-SHA256, 7-day expiry, Jwt:SigningKey in .secrets/env). POST /api/auth/register + /api/auth/login -> {token, email, expiresAt}. SnippetsController fully [Authorize]; executions allow guests but record UserId when a token is present. GET /api/executions/mine = per-user history (paginated, latest first).
- [x] Frontend auth: /auth route (login/register card, same visual style as home), AuthService (token in localStorage, user$ BehaviorSubject), authInterceptor attaches Bearer to /api requests. Editor toolbar shows user email + logout when logged in, "Login to save" link when guest. Side pane gets Run/Snippets/History tabs when logged in: save current editor content as a titled snippet, load/delete snippets, browse + restore past executions (sourceCode + stdin restored into the editor). ExecutionResponse now includes sourceCode/standardInput to support restore. Home page: "Start coding" (guest) + "Login" buttons.
- [x] **Oracle VM deployment**: Migrated from Azure VM to Oracle Linux 9.8 VM. Uses nginx (instead of Caddy) for HTTPS/reverse proxy, SQLite (instead of Azure SQL) for database, Podman (instead of Docker) for containerization. SELinux configured for container access.

## Next steps (priority order)
1. Full IntelliSense for other languages: add clangd (C/C++), csharp-ls, fsautocomplete to LspBridge.Servers + frontend languageId mapping. Same LspClient/monaco-lsp wiring — just register per language.
2. Warm container pool: `docker exec` into pre-warmed containers (~0.19s vs ~0.9s spin-up); needs between-run hygiene (wipe /work+/tmp, kill stray PIDs) and pool lifecycle management
3. Shareable snippet links: public URL /s/{id} for snippets (needs an IsPublic flag + unauthenticated GET endpoint)
4. AI add-on: endpoint that sends failed executions (source + stderr) to an LLM for error explanations (the agentic feature)
5. Add Haskell to LanguageRegistry (requires ghc install)
6. Migrate to Oracle PostgreSQL: set up VCN peering or move VM to same VCN as the PostgreSQL database

## Environment
- **Oracle VM**: Oracle Linux 9.8, 8 vCPU / 30GB RAM, 6GB swap (much more powerful than previous hosts)
- **.NET SDK 8.0**: installed via dnf, at /usr/bin/dotnet (not /usr/local/dotnet); dotnet-ef at ~/.dotnet/tools
- **Node.js 22**: installed via dnf module enable nodejs:22
- **Containers**: Podman 5.8.2 with podman-docker compatibility (docker command available via podman)
- **Languages**: python3, g++ 13 available via dnf; NO ghc
- **Web server**: nginx 1.20.1 (instead of Caddy), configured at /etc/nginx/conf.d/codeforge.conf
- **SSL**: Let's Encrypt via certbot (installed via pip, not in Oracle repos)
- **SELinux**: Enforcing, httpd_can_network_connect=1 for nginx -> backend proxy, container_file_t for mounted dirs
- **Firewall**: firewalld active, allow 22/80/443 only; Oracle Cloud NSGs also configured for inbound 22/80/443
- **Database**: SQLite (local file `/home/opc/codeforge/.secrets/codeforge.db`), migrated from Azure SQL
- **Devin CLI permissions**: global blanket allow (exec/edit/Write/**/Fetch) in ~/.config/devin/config.json

## Candidate languages to add (assessed)
- Go: easy (single binary, `go run`) | Ruby/PHP: trivial via apt | Clojure: use Babashka (ms startup, not JVM Clojure)
- Scala/Java/Kotlin: JVM-heavy, slow on this box — defer | Haskell: needs ghc install
