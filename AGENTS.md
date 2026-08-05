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

## Progress (as of 2026-08-05)
DONE:
- [x] .NET 8 backend: Api / Core / Infrastructure layers, xUnit tests (21 passing)
- [x] SignalR live streaming: output chunks pushed as produced (verified ~1s apart over ws through the proxy), polling kept as fallback
- [x] Execution engine: channel queue, background worker, timeouts, output caps, temp-dir cleanup
- [x] Docker isolation: DockerRunner with no-network/read-only/resource-capped containers, verified (network blocked, host FS invisible, OOM kill, no leftover containers); LocalProcessRunner kept as dev fallback
- [x] Languages: python, cpp, csharp, fsharp, typescript (tsx) — versions detected at runtime via LanguageInfoService (cached, shown in UI dropdown)
- [x] Angular 17 frontend: Monaco editor, per-language samples, stdin box, status chips, stdout/stderr panels, /api proxy, light/dark theme toggle (localStorage)
- [x] End-to-end verified through the Angular proxy (4200 -> 5045) with Docker runner active
- [x] Python LSP (Pyright) IntelliSense: backend LspBridge maps /lsp/{language} WebSocket -> pyright-langserver stdio (one server process per browser session, killed on disconnect); frontend LspClient + monaco-lsp wiring (completion, hover, diagnostics markers); lazily started when Python is selected. Verified initialize handshake through the proxy.
- [x] Persistence: Azure SQL Database via EF Core 8 + Microsoft.EntityFrameworkCore.SqlServer. CodeForgeDbContext (IdentityDbContext<ApplicationUser>) with Executions + Snippets tables; DesignTimeDbContextFactory reads ConnectionStrings__CodeForge env var or .secrets/dbconnectionstring.txt for dotnet-ef. EfExecutionStore (IDbContextFactory-based, safe for the singleton ExecutionWorker) replaces InMemoryExecutionStore when a connection string is configured; worker persists Running + final states. Secrets in /root/codeforge/.secrets/env (gitignored, chmod 600), loaded by codeforge-api.service via EnvironmentFile. NOTE: DB is Azure SQL serverless — first connection after idle wakes it up and can fail with error 40613; just retry.
- [x] Auth: ASP.NET Core Identity (email+password, min 8 chars) + JWT bearer (HMAC-SHA256, 7-day expiry, Jwt:SigningKey in .secrets/env). POST /api/auth/register + /api/auth/login -> {token, email, expiresAt}. SnippetsController fully [Authorize]; executions allow guests but record UserId when a token is present. GET /api/executions/mine = per-user history (paginated, latest first).
- [x] Frontend auth: /auth route (login/register card, same visual style as home), AuthService (token in localStorage, user$ BehaviorSubject), authInterceptor attaches Bearer to /api requests. Editor toolbar shows user email + logout when logged in, "Login to save" link when guest. Side pane gets Run/Snippets/History tabs when logged in: save current editor content as a titled snippet, load/delete snippets, browse + restore past executions (sourceCode + stdin restored into the editor). ExecutionResponse now includes sourceCode/standardInput to support restore. Home page: "Start coding" (guest) + "Login" buttons.

## Next steps (priority order)
0. Full IntelliSense for other languages: add clangd (C/C++), csharp-ls, fsautocomplete to LspBridge.Servers + frontend languageId mapping. Same LspClient/monaco-lsp wiring — just register per language.
0.5. Warm container pool: `docker exec` into pre-warmed containers (~0.19s vs ~0.9s spin-up); needs between-run hygiene (wipe /work+/tmp, kill stray PIDs) and pool lifecycle management
1. ~~Persistence~~ DONE (Azure SQL + EF Core + Identity, see above)
2. Production deploy: domain + Caddy (auto TLS) on this droplet, serve `ng build` static files, proxy /api+/hubs (ws) to API. NOT DO App Platform — it has no Docker socket, execution engine can't run there. Add rate-limit before public exposure (auth is done).
3. Shareable snippet links: public URL /s/{id} for snippets (needs an IsPublic flag + unauthenticated GET endpoint)
4. AI add-on: endpoint that sends failed executions (source + stderr) to an LLM for error explanations (the agentic feature)
5. Add Haskell to LanguageRegistry (requires ghc install)

## Environment
- DigitalOcean droplet, 2 vCPU / 4GB RAM, 2GB swap at /swapfile
- .NET SDK 8.0 at /usr/local/dotnet; dotnet-ef at /root/.dotnet/tools (needs DOTNET_ROOT=/usr/local/dotnet); node 22; tsx + tsc installed globally (npm); python3; g++ 13; Docker 29 installed; NO ghc
- Docker images: python:3.12-slim, gcc:13, mcr.microsoft.com/dotnet/sdk:8.0, codeforge-typescript (local build, docker/typescript/Dockerfile)
- DB: Azure SQL Database (serverless) at jaychoudharydb.database.windows.net, database CodeForgeDB. Droplet IP is firewall-allowlisted. EF Core migrations applied via dotnet-ef.
- Devin CLI permissions: global blanket allow (exec/edit/Write/**/Fetch) in ~/.config/devin/config.json

## Candidate languages to add (assessed)
- Go: easy (single binary, `go run`) | Ruby/PHP: trivial via apt | Clojure: use Babashka (ms startup, not JVM Clojure)
- Scala/Java/Kotlin: JVM-heavy, slow on this box — defer | Haskell: needs ghc install
