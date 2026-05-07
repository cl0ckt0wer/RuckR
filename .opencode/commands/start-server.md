---
name: start-server
description: Starts RuckR dev environment — Aspire Dashboard + Server + optional browser
category: dev
---

# Start RuckR Development Environment

Starts the full RuckR development stack:

1. **Aspire Dashboard** (Docker) — OTLP traces, metrics, structured logs at `http://localhost:18888`
2. **RuckR Server** (.NET 10) — Blazor WASM app at `https://localhost:7161`
3. **Browser** (optional) — opens `/map` page

All server output is captured to `docs/plan/20260506-server-startup-jaeger/logs/` for troubleshooting.

## Usage

Invoke the PowerShell script from the workspace root:

```powershell
.\scripts\start-ruckr.ps1
```

With flags:
```powershell
.\scripts\start-ruckr.ps1 -NoBrowser     # skip browser launch
.\scripts\start-ruckr.ps1 -SkipBuild     # skip dotnet build
```

## Verification

After starting, check:
- **App**: `https://localhost:7161/map` — Blazor WASM should load without 404 errors
- **OTEL**: `http://localhost:18888/traces` — trace spans from HTTP requests, EF Core queries
- **Health**: `https://localhost:7161/api/telemetry/health` — should return 200
- **Status**: `https://localhost:7161/api/telemetry/status` — OTEL pipeline status

## Stopping

```powershell
.\scripts\stop-ruckr.ps1
```

## For the Orchestrator Agent

When this command is invoked, you MUST:
1. Verify Docker is running: `docker ps`
2. Execute: `powershell -ExecutionPolicy Bypass -File .\scripts\start-ruckr.ps1`
3. Report back the summary URLs and whether the server started successfully
4. Check the log file if startup fails
