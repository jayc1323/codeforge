# CodeForge

[![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Angular 17](https://img.shields.io/badge/Angular-17-DD0031?logo=angular)](https://angular.io/)
[![Docker](https://img.shields.io/badge/sandboxed%20with-Docker-2496ED?logo=docker)](https://www.docker.com/)
[![License: MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE)

**[Live demo → coderunner.duckdns.org](https://coderunner.duckdns.org)** · **[Report an issue](https://github.com/jayc1323/codeforge/issues)**

An online multi-language code execution platform. Write code in the browser (Monaco — the editor that powers VS Code), run it in an isolated Docker container, and watch output stream back live over WebSocket. Create an account to save snippets and keep your execution history.

## Languages

Python · C++ · C# · F# · TypeScript — each with runtime-detected version display and official docs links.

**IntelliSense:** Python gets real autocomplete, hover docs, and inline diagnostics via [Pyright](https://github.com/microsoft/pyright) over a custom LSP↔WebSocket bridge; TypeScript uses Monaco's built-in language service.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ Browser (Angular 17 + Monaco)                                   │
│   editor · stdin · live output · snippets · history · auth      │
└──────┬────────────────────┬──────────────────┬──────────────────┘
       │ REST /api/*        │ WS /hubs/*       │ WS /lsp/python
       ▼                    ▼                  ▼
┌─────────────────────────────────────────────────────────────────┐
│ Caddy (TLS, static files, reverse proxy)                        │
└─────────────────────────────────────────────────────────────────┘
       ▼
┌─────────────────────────────────────────────────────────────────┐
│ CodeForge.Api (ASP.NET Core 8)                                  │
│   Executions · Languages · Auth (Identity+JWT) · Snippets       │
│   ExecutionHub (SignalR) · LspBridge (WebSocket → LSP stdio)    │
│   Rate limiting (per-IP)                                        │
└──────┬──────────────────────────────────────────────────────────┘
       │ interfaces only
       ▼
┌──────────────────────────────┬──────────────────────────────────┐
│ CodeForge.Core (domain)      │ Persistence (EF Core 8)          │
│   LanguageRegistry           │   Azure SQL Database             │
│   IExecutionRunner/Queue/…   │   Executions · Snippets · Users  │
└──────▲───────────────────────┴──────────────────────────────────┘
       │ implements
┌─────────────────────────────────────────────────────────────────┐
│ CodeForge.Infrastructure                                        │
│   ExecutionQueue (Channel) · ExecutionWorker (BackgroundService)│
│   DockerRunner · LocalProcessRunner (dev) · EfExecutionStore    │
└──────┬──────────────────────────────────────────────────────────┘
       │ docker run (per execution)
       ▼
┌─────────────────────────────────────────────────────────────────┐
│ Throwaway container per execution                               │
│   --network none · 256MB RAM (no swap) · 1 CPU · 128 PIDs       │
│   read-only rootfs · cap-drop ALL · no-new-privileges           │
│   unprivileged user · compile+run script with phase timeouts    │
└─────────────────────────────────────────────────────────────────┘
```

### Execution flow

1. `POST /api/executions` → `202 Accepted` + execution id; record persisted (with user id when authenticated) and queued on an in-memory channel
2. `ExecutionWorker` dequeues, marks Running, invokes the configured `IExecutionRunner`
3. `DockerRunner` writes the submission to a temp dir, generates a phase script (`compile` → `run`, each with its own `timeout`), and starts a hardened container with only that dir mounted
4. As the program prints, stdout/stderr chunks flow back live: `IExecutionProgress` → worker → `IExecutionEventPublisher` → SignalR group → browser (with 500ms polling as an automatic fallback)
5. Phase attribution (compile error vs run timeout vs runtime failure) uses marker files + wall-clock disambiguation of GNU `timeout`'s exit-code-124 ambiguity
6. Final state (status, capped output, exit code, duration) is persisted to Azure SQL and available via `GET /api/executions/{id}`

### LSP bridge

`LspBridge` maps `wss://…/lsp/python` to a `pyright-langserver --stdio` process: it strips LSP's `Content-Length` framing from process output (one JSON-RPC payload per WebSocket message) and re-adds it on the way in. One language-server process per browser session, killed on disconnect. Adding another language is a one-line entry in the bridge's server map plus its language-id on the frontend.

### Why two runners?

`IExecutionRunner` has two implementations behind one interface, selected by config (`Execution:Runner`): **DockerRunner** (production — every run is isolated, no network, hard resource caps, zero trace after) and **LocalProcessRunner** (dev fallback — runs directly on the host where Docker isn't available).

## Stack

| Layer | Tech |
|---|---|
| Frontend | Angular 17, Monaco editor, @microsoft/signalr |
| Backend | .NET 8 ASP.NET Core, SignalR, Channel-based queue, rate limiting |
| Auth | ASP.NET Core Identity, JWT bearer (HMAC-SHA256) |
| Persistence | EF Core 8, Azure SQL Database |
| Intelligence | Pyright (LSP) via custom WebSocket bridge |
| Isolation | Docker (per-language images, custom TypeScript image) |
| Serving | Caddy (auto Let's Encrypt TLS), systemd, UFW |
| Tests | xUnit (21 tests: runner behavior, isolation guarantees, streaming) |

## Run (dev)

```bash
# backend (http://localhost:5045)
cd backend/src/CodeForge.Api && dotnet run

# frontend (http://localhost:80, proxies /api, /hubs, /lsp to the API)
cd frontend && npx ng serve --host 0.0.0.0 --port 80

# tests
cd backend && dotnet test
```

Requires: .NET 8 SDK, Node 22, Docker (for the sandboxed runner), and per-language toolchains for the local runner.

## Deploy (production)

```bash
./deploy/deploy-frontend.sh   # ng build → /var/www/codeforge (served by Caddy)
./deploy/deploy-backend.sh    # dotnet publish → /opt/codeforge-api (systemd, health-checked)
```

Caddy config lives in `deploy/Caddyfile` (copy to `/etc/caddy/Caddyfile`, then `systemctl reload caddy`). Secrets (DB connection string, JWT key) are read from `.secrets/env`, which is gitignored.

## Roadmap

- [ ] LSP for the remaining languages (clangd, csharp-ls, fsautocomplete — bridge is ready)
- [ ] Shareable public snippet links
- [ ] Warm container pool for sub-200ms execution startup
- [ ] AI-assisted error explanations
- [ ] More languages (Go, Rust, Ruby)

## License

[MIT](LICENSE)
