---
name: publish-exe-dev
description: Publish RuckR Server to the exe.dev production VM (ruckr.exe.xyz)
category: deploy
---

# Publish to exe.dev

Publishes the RuckR Server to the production VM at `https://ruckr.exe.xyz`:

1. **Publish** — `dotnet publish` as framework-dependent `linux-x64` to `publish/` (requires .NET 10 runtime on the VM)
2. **Compress** — Tars the published output into a single archive
3. **Deploy** — SCPs the archive to `ruckr.exe.xyz`, extracts into an immutable release directory, atomically switches the `current` symlink
4. **Restart** — Restarts the `ruckr.service` systemd unit (port 5000, proxied by exe.dev)
5. **Verify** — Checks the health endpoint at `https://ruckr.exe.xyz/api/telemetry/health`

## Prerequisites

- SSH access to `ruckr.exe.xyz` (key-based)
- .NET 10 SDK installed locally
- .NET 10 ASP.NET runtime installed on the VM (`dotnet --list-runtimes`) — auto-installed if missing
- Docker on the VM (SQL Server + Jaeger containers) — auto-started if missing
- SCP, tar, curl available on PATH
- `ConnectionStrings:RuckRDbContext` set in user-secrets for `RuckR.Server.csproj` (password auto-discovered)

## Usage

**v2 (bash, recommended):**
```bash
./scripts/publish-exe-dev.sh
```

With flags:
```bash
./scripts/publish-exe-dev.sh --skip-build       # skip dotnet publish (use existing publish/)
./scripts/publish-exe-dev.sh --skip-restart      # skip server restart (deploy files only)
./scripts/publish-exe-dev.sh --yes               # non-interactive mode
```

**v1 (PowerShell, legacy):**
```powershell
$env:RUCKR_DB_PASSWORD = "the-real-sa-password"
.\scripts\publish-exe-dev.ps1
```

With flags:
```powershell
.\scripts\publish-exe-dev.ps1 -SkipBuild      # skip dotnet publish (use existing publish/)
.\scripts\publish-exe-dev.ps1 -SkipRestart    # skip server restart (deploy files only)
```

## For the Orchestrator Agent

When this command is invoked, you MUST:
1. Execute: `./scripts/publish-exe-dev.sh` (the bash v2 script auto-discovers the DB password from `dotnet user-secrets` automatically)
2. Report back: release ID, whether health check passed, and the app URL
3. If SSH fails, ask the user to verify SSH credentials and connectivity
4. Check `https://ruckr.exe.xyz` in a browser if possible
