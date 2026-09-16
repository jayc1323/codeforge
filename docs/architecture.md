# CodeForge Architecture

## System Overview

```
                                    INTERNET
                                        │
                                        │ HTTPS (443)
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           ORACLE CLOUD INFRASTRUCTURE                            │
│                                                                                  │
│  ┌────────────────────────────────────────────────────────────────────────────┐ │
│  │                    Oracle Linux 9.8 VM (150.136.57.158)                    │ │
│  │                         8 vCPU / 30GB RAM / 6GB Swap                       │ │
│  │                                                                            │ │
│  │  ┌──────────────────────────────────────────────────────────────────────┐  │ │
│  │  │                        FIREWALLD (Host Firewall)                     │  │ │
│  │  │                     Allows: 22 (SSH), 80, 443 only                   │  │ │
│  │  └──────────────────────────────────────────────────────────────────────┘  │ │
│  │                                    │                                       │ │
│  │                                    ▼                                       │ │
│  │  ┌──────────────────────────────────────────────────────────────────────┐  │ │
│  │  │                         NGINX 1.20.1                                 │  │ │
│  │  │                    (Reverse Proxy + Static Files)                    │  │ │
│  │  │                                                                      │  │ │
│  │  │   ┌─────────────────────────────────────────────────────────────┐   │  │ │
│  │  │   │                    TLS TERMINATION                          │   │  │ │
│  │  │   │         Let's Encrypt Certificate (certbot)                 │   │  │ │
│  │  │   │         Domain: coderunner.duckdns.org                      │   │  │ │
│  │  │   │         Protocols: TLSv1.2, TLSv1.3                         │   │  │ │
│  │  │   └─────────────────────────────────────────────────────────────┘   │  │ │
│  │  │                                                                      │  │ │
│  │  │   ROUTING RULES:                                                     │  │ │
│  │  │   ┌─────────────────┬──────────────────────────────────────────┐    │  │ │
│  │  │   │ /               │ Static files from /var/www/codeforge     │    │  │ │
│  │  │   │                 │ (Angular SPA, fallback to index.html)    │    │  │ │
│  │  │   ├─────────────────┼──────────────────────────────────────────┤    │  │ │
│  │  │   │ /api/*          │ Proxy to localhost:5045 (HTTP/1.1)       │    │  │ │
│  │  │   ├─────────────────┼──────────────────────────────────────────┤    │  │ │
│  │  │   │ /hubs/*         │ Proxy + WebSocket upgrade (SignalR)      │    │  │ │
│  │  │   ├─────────────────┼──────────────────────────────────────────┤    │  │ │
│  │  │   │ /lsp/*          │ Proxy + WebSocket upgrade (LSP bridge)   │    │  │ │
│  │  │   └─────────────────┴──────────────────────────────────────────┘    │  │ │
│  │  └──────────────────────────────────────────────────────────────────────┘  │ │
│  │                 │                    │                    │                │ │
│  │      Static     │        REST        │      WebSocket     │                │ │
│  │      Files      │        API         │      Connections   │                │ │
│  │         │       │          │         │          │         │                │ │
│  │         ▼       │          ▼         │          ▼         │                │ │
│  │  ┌──────────┐   │   ┌─────────────────────────────────────────────────┐   │ │
│  │  │ Angular  │   │   │              CODEFORGE.API                      │   │ │
│  │  │   SPA    │   │   │         ASP.NET Core 8 (Kestrel)                │   │ │
│  │  │          │   │   │           localhost:5045                        │   │ │
│  │  │ /var/www │   │   │                                                 │   │ │
│  │  │/codeforge│   │   │  ┌─────────────────────────────────────────┐   │   │ │
│  │  │          │   │   │  │              CONTROLLERS                 │   │   │ │
│  │  │ - Monaco │   │   │  │  ExecutionsController  POST/GET /api/ex │   │   │ │
│  │  │ - SignalR│   │   │  │  LanguagesController   GET /api/langs   │   │   │ │
│  │  │   Client │   │   │  │  AuthController        POST /api/auth/* │   │   │ │
│  │  │ - LSP    │   │   │  │  SnippetsController    CRUD /api/snip   │   │   │ │
│  │  │   Client │   │   │  └─────────────────────────────────────────┘   │   │ │
│  │  └──────────┘   │   │                      │                         │   │ │
│  │                 │   │  ┌───────────────────┼───────────────────┐     │   │ │
│  │                 │   │  │                   │                   │     │   │ │
│  │                 │   │  ▼                   ▼                   ▼     │   │ │
│  │                 │   │ ┌────────┐    ┌───────────┐    ┌───────────┐  │   │ │
│  │                 │   │ │SignalR │    │ Execution │    │    LSP    │  │   │ │
│  │                 │   │ │  Hub   │    │   Queue   │    │  Bridge   │  │   │ │
│  │                 │   │ │        │    │ (Channel) │    │           │  │   │ │
│  │                 │   │ └────────┘    └─────┬─────┘    └─────┬─────┘  │   │ │
│  │                 │   │      │              │                │        │   │ │
│  │                 │   │      │              ▼                ▼        │   │ │
│  │                 │   │      │        ┌───────────┐    ┌───────────┐  │   │ │
│  │                 │   │      │        │ Execution │    │  Pyright  │  │   │ │
│  │                 │   │      │        │  Worker   │    │ Language  │  │   │ │
│  │                 │   │      │        │(Background│    │  Server   │  │   │ │
│  │                 │   │      │        │ Service)  │    │  (stdio)  │  │   │ │
│  │                 │   │      │        └─────┬─────┘    └───────────┘  │   │ │
│  │                 │   │      │              │                         │   │ │
│  │                 │   │      │              ▼                         │   │ │
│  │                 │   │      │        ┌───────────┐                   │   │ │
│  │                 │   │      │        │  Docker   │                   │   │ │
│  │                 │   │      │        │  Runner   │                   │   │ │
│  │                 │   │      │        └─────┬─────┘                   │   │ │
│  │                 │   │      │              │                         │   │ │
│  │                 │   │      ▼              ▼                         │   │ │
│  │                 │   │  ┌──────────────────────────────────────┐    │   │ │
│  │                 │   │  │           EF CORE 8 + SQLITE         │    │   │ │
│  │                 │   │  │    /home/opc/codeforge/.secrets/     │    │   │ │
│  │                 │   │  │           codeforge.db               │    │   │ │
│  │                 │   │  │  Tables: Users, Executions, Snippets │    │   │ │
│  │                 │   │  └──────────────────────────────────────┘    │   │ │
│  │                 │   └─────────────────────────────────────────────────┘   │ │
│  │                 │                         │                               │ │
│  │                 │                         ▼                               │ │
│  │                 │   ┌─────────────────────────────────────────────────┐   │ │
│  │                 │   │                 PODMAN 5.8.2                    │   │ │
│  │                 │   │            (Docker-compatible CLI)              │   │ │
│  │                 │   │                                                 │   │ │
│  │                 │   │   Throwaway containers per execution:           │   │ │
│  │                 │   │   ┌─────────────────────────────────────────┐   │   │ │
│  │                 │   │   │  ISOLATION FLAGS:                       │   │   │ │
│  │                 │   │   │  --network none     (no internet)       │   │   │ │
│  │                 │   │   │  --memory 256m      (hard RAM limit)    │   │   │ │
│  │                 │   │   │  --cpus 1           (1 CPU core)        │   │   │ │
│  │                 │   │   │  --pids-limit 128   (process limit)     │   │   │ │
│  │                 │   │   │  --read-only        (immutable rootfs)  │   │   │ │
│  │                 │   │   │  --cap-drop ALL     (no capabilities)   │   │   │ │
│  │                 │   │   │  --user 65534       (nobody user)       │   │   │ │
│  │                 │   │   │  --security-opt no-new-privileges       │   │   │ │
│  │                 │   │   └─────────────────────────────────────────┘   │   │ │
│  │                 │   │                                                 │   │ │
│  │                 │   │   CONTAINER IMAGES:                             │   │ │
│  │                 │   │   ├── python:3.12-slim     (Python)             │   │ │
│  │                 │   │   ├── gcc:13               (C++)                │   │ │
│  │                 │   │   ├── dotnet/sdk:8.0       (C#, F#)             │   │ │
│  │                 │   │   └── localhost/codeforge-typescript (TS)       │   │ │
│  │                 │   └─────────────────────────────────────────────────┘   │ │
│  │                 │                                                         │ │
│  └─────────────────┼─────────────────────────────────────────────────────────┘ │
│                    │                                                           │
│  ┌─────────────────┼─────────────────────────────────────────────────────────┐ │
│  │                 │           ORACLE CLOUD NETWORKING                       │ │
│  │                 │                                                         │ │
│  │   NSG/Security List: Ingress TCP 22, 80, 443 from 0.0.0.0/0              │ │
│  │   DuckDNS: coderunner.duckdns.org -> 150.136.57.158                      │ │
│  │                                                                           │ │
│  │   ┌─────────────────────────────────────────────────────────────────┐    │ │
│  │   │  Private Subnet (10.0.207.0/24) - PostgreSQL DB (future use)   │    │ │
│  │   │  PostgreSQL 17.6 @ 10.0.207.245 (not currently connected)      │    │ │
│  │   └─────────────────────────────────────────────────────────────────┘    │ │
│  └───────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────────┘
```

## Request-Response Lifecycle

### 1. Code Execution Request

```
┌──────────┐     ┌─────────┐     ┌──────────────┐     ┌────────────┐     ┌───────────┐
│  Browser │     │  nginx  │     │ CodeForge.Api│     │  Execution │     │  Podman   │
│ (Angular)│     │         │     │              │     │   Worker   │     │ Container │
└────┬─────┘     └────┬────┘     └──────┬───────┘     └─────┬──────┘     └─────┬─────┘
     │                │                 │                   │                  │
     │ 1. POST /api/executions          │                   │                  │
     │    {language, sourceCode, stdin} │                   │                  │
     │───────────────>│                 │                   │                  │
     │                │                 │                   │                  │
     │                │ 2. Proxy to     │                   │                  │
     │                │    localhost:5045                   │                  │
     │                │────────────────>│                   │                  │
     │                │                 │                   │                  │
     │                │                 │ 3. Validate &     │                  │
     │                │                 │    Queue execution│                  │
     │                │                 │──────────────────>│                  │
     │                │                 │                   │                  │
     │                │ 4. 202 Accepted │                   │                  │
     │                │    {id: "..."}  │                   │                  │
     │<───────────────│<────────────────│                   │                  │
     │                │                 │                   │                  │
     │ 5. Connect WebSocket /hubs/executions                │                  │
     │═══════════════>│═══════════════>│                   │                  │
     │                │                 │                   │                  │
     │                │                 │                   │ 6. Dequeue &     │
     │                │                 │                   │    create container
     │                │                 │                   │─────────────────>│
     │                │                 │                   │                  │
     │                │                 │                   │ 7. Mount /work,  │
     │                │                 │                   │    run script    │
     │                │                 │                   │<─────────────────│
     │                │                 │                   │                  │
     │                │                 │ 8. Stream stdout/ │                  │
     │                │                 │    stderr chunks  │                  │
     │<═══════════════│<═══════════════│<──────────────────│                  │
     │  (SignalR)     │                 │                   │                  │
     │                │                 │                   │ 9. Exit code,    │
     │                │                 │                   │    cleanup       │
     │                │                 │                   │<─────────────────│
     │                │                 │                   │                  │
     │                │                 │ 10. Final status  │                  │
     │<═══════════════│<═══════════════│<──────────────────│                  │
     │  (SignalR)     │                 │   (persist to DB) │                  │
     │                │                 │                   │                  │
```

### 2. Python IntelliSense Request

```
┌──────────┐     ┌─────────┐     ┌──────────────┐     ┌───────────┐
│  Browser │     │  nginx  │     │ CodeForge.Api│     │  Pyright  │
│ (Monaco) │     │         │     │  (LspBridge) │     │ (stdio)   │
└────┬─────┘     └────┬────┘     └──────┬───────┘     └─────┬─────┘
     │                │                 │                   │
     │ 1. WebSocket connect /lsp/python │                   │
     │═══════════════>│═══════════════>│                   │
     │                │                 │                   │
     │                │                 │ 2. Spawn pyright  │
     │                │                 │    --stdio        │
     │                │                 │──────────────────>│
     │                │                 │                   │
     │ 3. LSP initialize                │                   │
     │    {capabilities...}             │                   │
     │───────────────>│────────────────>│ 4. Add Content-   │
     │                │                 │    Length header  │
     │                │                 │──────────────────>│
     │                │                 │                   │
     │                │                 │ 5. Response       │
     │                │                 │<──────────────────│
     │                │                 │ 6. Strip header,  │
     │<───────────────│<────────────────│    send JSON      │
     │                │                 │                   │
     │ 7. textDocument/completion       │                   │
     │    {position, uri}               │                   │
     │───────────────>│────────────────>│──────────────────>│
     │                │                 │                   │
     │                │                 │ 8. Completion     │
     │                │                 │    items          │
     │<───────────────│<────────────────│<──────────────────│
     │                │                 │                   │
     │ (connection stays open for       │                   │
     │  hover, diagnostics, etc.)       │                   │
     │                │                 │                   │
```

### 3. Static Asset Request

```
┌──────────┐     ┌─────────┐     ┌──────────────────┐
│  Browser │     │  nginx  │     │ /var/www/codeforge│
└────┬─────┘     └────┬────┘     └────────┬─────────┘
     │                │                   │
     │ GET /          │                   │
     │───────────────>│                   │
     │                │ try_files         │
     │                │──────────────────>│
     │                │                   │
     │                │ index.html        │
     │<───────────────│<──────────────────│
     │                │                   │
     │ GET /main-*.js │                   │
     │───────────────>│                   │
     │                │ Serve with        │
     │                │ Cache-Control:    │
     │                │ max-age=1y        │
     │<───────────────│<──────────────────│
     │                │                   │
     │ GET /app       │                   │
     │ (SPA route)    │                   │
     │───────────────>│                   │
     │                │ try_files fails,  │
     │                │ fallback to       │
     │                │ index.html        │
     │<───────────────│<──────────────────│
     │                │                   │
```

## Component Details

### nginx Configuration Summary

| Location | Handler | Protocol | Purpose |
|----------|---------|----------|---------|
| `/` | Static files | HTTPS | Angular SPA + assets |
| `/api/*` | Proxy | HTTP/1.1 | REST API endpoints |
| `/hubs/*` | Proxy + WS | HTTP/1.1 | SignalR real-time |
| `/lsp/*` | Proxy + WS | HTTP/1.1 | Language server protocol |

### Security Layers

```
┌─────────────────────────────────────────────────────────────┐
│ Layer 1: Oracle Cloud NSG                                   │
│   - Only ports 22, 80, 443 open                            │
│   - All other inbound traffic blocked                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│ Layer 2: firewalld (Host)                                   │
│   - Mirrors NSG rules                                       │
│   - Defense in depth                                        │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│ Layer 3: TLS (nginx)                                        │
│   - Let's Encrypt certificate                               │
│   - TLSv1.2/1.3 only                                        │
│   - HTTP -> HTTPS redirect                                  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│ Layer 4: Application (ASP.NET Core)                         │
│   - Rate limiting (10 exec/min, 10 auth/5min per IP)        │
│   - JWT authentication (HMAC-SHA256)                        │
│   - Input validation                                        │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│ Layer 5: Container Isolation (Podman)                       │
│   - No network access                                       │
│   - Resource limits (256MB RAM, 1 CPU, 128 PIDs)           │
│   - Read-only filesystem                                    │
│   - Dropped capabilities                                    │
│   - Unprivileged user (nobody)                             │
│   - SELinux context (container_file_t)                     │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow Summary

```
User Code Submission
        │
        ▼
┌───────────────┐
│   Validate    │ Language, size limits, rate limit
└───────┬───────┘
        │
        ▼
┌───────────────┐
│  Persist to   │ SQLite: Executions table
│   Database    │ Status: Queued
└───────┬───────┘
        │
        ▼
┌───────────────┐
│   Enqueue     │ System.Threading.Channels
└───────┬───────┘
        │
        ▼
┌───────────────┐
│   Dequeue &   │ BackgroundService
│   Execute     │ Status: Running
└───────┬───────┘
        │
        ▼
┌───────────────┐
│  Create temp  │ /tmp/codeforge-{guid}/
│   directory   │ source file + run script
└───────┬───────┘
        │
        ▼
┌───────────────┐
│ Run container │ podman run --rm -i ...
│  with limits  │ Mount temp dir at /work
└───────┬───────┘
        │
        ├──────────────────┐
        │                  │
        ▼                  ▼
┌───────────────┐  ┌───────────────┐
│ Stream output │  │  Wait for     │
│ via SignalR   │  │  completion   │
└───────────────┘  └───────┬───────┘
                           │
                           ▼
                   ┌───────────────┐
                   │ Parse exit    │ Compile error vs
                   │ code & markers│ runtime error vs timeout
                   └───────┬───────┘
                           │
                           ▼
                   ┌───────────────┐
                   │  Persist      │ Status: Completed/Failed/
                   │  final state  │ TimedOut/CompileError
                   └───────┬───────┘
                           │
                           ▼
                   ┌───────────────┐
                   │ Cleanup temp  │ Remove directory
                   │  directory    │ Container auto-removed
                   └───────────────┘
```
