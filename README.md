# CodeForge

An online multi-language code execution platform. Write code in the browser (Monaco — the editor that powers VS Code), run it in an isolated Docker container, and watch the output stream back live over WebSocket.

## Languages

Python · C++ · C# · F# · TypeScript — each with runtime-detected version display and official docs links. TypeScript additionally has full in-browser IntelliSense (types, autocomplete, diagnostics) via Monaco's built-in language service.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ Browser (Angular 17 + Monaco)                                   │
│   editor · stdin · live output panel · theme toggle             │
└──────┬───────────────────────────────────┬──────────────────────┘
       │ REST /api/*                        │ WebSocket /hubs/executions
       ▼                                    ▼
┌─────────────────────────────────────────────────────────────────┐
│ CodeForge.Api (ASP.NET Core 8)                                  │
│   ExecutionsController · LanguagesController · ExecutionHub     │
│   SignalRExecutionEventPublisher (IExecutionEventPublisher)     │
└──────┬──────────────────────────────────────────────────────────┘
       │ depends on interfaces only
       ▼
┌─────────────────────────────────────────────────────────────────┐
│ CodeForge.Core (domain)                                         │
│   LanguageRegistry (compile/run/version/docs per language)      │
│   IExecutionRunner · IExecutionQueue · IExecutionStore          │
│   IExecutionProgress · IExecutionEventPublisher                 │
└──────▲──────────────────────────────────────────────────────────┘
       │ implements
┌─────────────────────────────────────────────────────────────────┐
│ CodeForge.Infrastructure                                        │
│   ExecutionQueue (Channel) · ExecutionWorker (BackgroundService)│
│   DockerRunner (default) · LocalProcessRunner (dev fallback)    │
│   LanguageInfoService (runtime version detection, cached)       │
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

1. `POST /api/executions` → `202 Accepted` + execution id, record queued on an in-memory channel
2. `ExecutionWorker` dequeues, marks Running, invokes the configured `IExecutionRunner`
3. `DockerRunner` writes the submission to a temp dir, generates a phase script (`compile` → `run`, each with its own `timeout`), and starts a hardened container with only that dir mounted
4. As the program prints, stdout/stderr chunks flow back live: `IExecutionProgress` → worker → `IExecutionEventPublisher` → SignalR group → browser
5. Phase attribution (compile error vs run timeout vs runtime failure) uses marker files + wall-clock disambiguation of GNU `timeout`'s exit-code-124 ambiguity
6. Final state (status, capped output, exit code, duration) is stored and available via `GET /api/executions/{id}` (also the fallback if SignalR is unavailable)

### Why two runners?

`IExecutionRunner` has two implementations behind one interface, selected by config (`Execution:Runner`): **DockerRunner** (production — every run is isolated, no network, hard resource caps, zero trace after) and **LocalProcessRunner** (dev fallback — runs directly on the host where Docker isn't available).

## Stack

| Layer | Tech |
|---|---|
| Frontend | Angular 17, Monaco editor, @microsoft/signalr |
| Backend | .NET 8 ASP.NET Core, SignalR, Channel-based queue |
| Isolation | Docker (per-language images, custom TypeScript image) |
| Tests | xUnit (21 tests: runner behavior, isolation guarantees, streaming) |

## Run

```bash
# backend (http://localhost:5045)
cd backend/src/CodeForge.Api && dotnet run

# frontend (http://localhost:4200, proxies /api and /hubs to the API)
cd frontend && npx ng serve

# tests
cd backend && dotnet test
```

Requires: .NET 8 SDK, Node 22, Docker (for the sandboxed runner), and per-language toolchains for the local runner.

## Roadmap

- [ ] Python IntelliSense via pyright (LSP over WebSocket) — then clangd, csharp-ls, fsautocomplete
- [ ] PostgreSQL persistence (execution history, saved snippets)
- [ ] Auth (Identity + JWT) and snippet sharing
- [ ] Warm container pool for sub-200ms execution startup
- [ ] AI-assisted error explanations
