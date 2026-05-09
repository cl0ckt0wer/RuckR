---
name: publish-exe-dev
description: Publish RuckR Server to the exe.dev production VM (ruckr.exe.xyz)
category: deploy
---

# Publish to exe.dev

Publishes the RuckR Server to the production VM at `https://ruckr.exe.xyz`:

1. **Publish** — `dotnet publish` as framework-dependent `linux-x64` to `publish/` (requires .NET 10 runtime on the VM)
2. **Stop** — Kills the existing server process on the VM
3. **Deploy** — SCPs the published files to `ruckr.exe.xyz:~/ruckr/`
4. **Start** — Restarts the server on port 8000 via nohup
5. **Verify** — Checks the health endpoint at `https://ruckr.exe.xyz/api/telemetry/health`

## Prerequisites

- SSH access to `ruckr.exe.xyz` (password or key)
- .NET 10 SDK installed locally
- .NET 10 ASP.NET runtime installed on the VM (`dotnet --list-runtimes`)
- SCP available on PATH

## Usage

```powershell
.\scripts\publish-exe-dev.ps1
```

With flags:
```powershell
.\scripts\publish-exe-dev.ps1 -SkipBuild      # skip dotnet publish (use existing publish/)
.\scripts\publish-exe-dev.ps1 -SkipRestart    # skip server restart (deploy files only)
```

## For the Orchestrator Agent

When this command is invoked, you MUST:
1. Execute: `powershell -ExecutionPolicy Bypass -File .\scripts\publish-exe-dev.ps1`
2. Report back whether the publish succeeded and the server is healthy
3. If SSH fails, ask the user to verify SSH credentials and connectivity
4. Check `https://ruckr.exe.xyz` in a browser if possible
