<#
.SYNOPSIS
    Starts the RuckR development environment: Aspire Dashboard (OTEL) + Server + optional browser.
.DESCRIPTION
    One command to rule them all. Starts:
    1. Aspire Dashboard (Docker) — OTLP traces/metrics/logs backend
    2. RuckR Server — .NET 10 ASP.NET Core with Blazor WASM
    3. Optionally opens a browser to the app
    All output is logged to docs/plan/20260506-server-startup-jaeger/logs/
.EXAMPLE
    .\scripts\start-ruckr.ps1
    .\scripts\start-ruckr.ps1 -NoBrowser
    .\scripts\start-ruckr.ps1 -SkipBuild
#>

param(
    [switch]$NoBrowser,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

# ── Ports ──
$dashboardUiPort = 18888
$dashboardOtlpPort = 18889
$dashboardHttpPort = 18890
$serverUrl = "https://localhost:7161"

# ── Logging ──
$logDir = Join-Path $repoRoot "docs/plan/20260506-server-startup-jaeger/logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$serverLog = Join-Path $logDir "server-$timestamp.log"

function Write-Step { param($msg) Write-Host "==> $msg" -ForegroundColor Cyan }

# ── Step 1: Aspire Dashboard (Docker) ──
Write-Step "Starting Aspire Dashboard on ports $dashboardUiPort (UI) / $dashboardOtlpPort (OTLP gRPC)"

# Stop any existing dashboard container
docker rm -f ruckr-aspire-dashboard 2>$null

docker run -d --name ruckr-aspire-dashboard `
    -p ${dashboardUiPort}:18888 `
    -p ${dashboardOtlpPort}:18889 `
    -p ${dashboardHttpPort}:18890 `
    mcr.microsoft.com/dotnet/aspire-dashboard:latest

Write-Host "  Dashboard UI:  http://localhost:$dashboardUiPort" -ForegroundColor Green
Write-Host "  OTLP gRPC:     http://localhost:$dashboardOtlpPort" -ForegroundColor Green

# ── Step 2: Build ──
if (-not $SkipBuild) {
    Write-Step "Building RuckR.sln"
    Push-Location $repoRoot
    dotnet build RuckR.sln
    if ($LASTEXITCODE -ne 0) {
        Write-Host "BUILD FAILED — check errors above" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    Pop-Location
} else {
    Write-Step "Skipping build (SkipBuild flag set)"
}

# ── Step 3: Start Server ──
Write-Step "Starting RuckR Server (log: $serverLog)"
Push-Location $repoRoot

$serverJob = Start-Job -Name RuckRServer -ScriptBlock {
    param($projectPath, $logPath)
    Set-Location $projectPath
    dotnet run --project "RuckR\Server\RuckR.Server.csproj" --launch-profile https 2>&1 |
        ForEach-Object {
            $line = "$(Get-Date -Format 'HH:mm:ss.fff') $_"
            Add-Content -Path $logPath -Value $line
            Write-Host $line
        }
} -ArgumentList $repoRoot, $serverLog

Pop-Location

# ── Step 4: Wait for readiness ──
Write-Step "Waiting for server to be ready..."
$ready = $false
for ($i = 0; $i -lt 60; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "$serverUrl/api/telemetry/health" `
            -SkipCertificateCheck -TimeoutSec 2 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            $ready = $true
            Write-Host "  Server ready after $($i+1)s" -ForegroundColor Green
            break
        }
    } catch {
        Start-Sleep -Seconds 1
    }
}

if (-not $ready) {
    Write-Host "  WARNING: Server not responding after 60s — check $serverLog" -ForegroundColor Yellow
}

# ── Step 5: Open browser ──
if (-not $NoBrowser -and $ready) {
    Write-Step "Opening browser to $serverUrl/map"
    Start-Process "$serverUrl/map"
}

# ── Step 6: Summary ──
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Magenta
Write-Host "  RuckR Dev Environment" -ForegroundColor Magenta
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Magenta
Write-Host "  App:             $serverUrl/map" -ForegroundColor Green
Write-Host "  OTEL Dashboard:  http://localhost:$dashboardUiPort" -ForegroundColor Green
Write-Host "  OTEL Traces:     http://localhost:$dashboardUiPort/traces" -ForegroundColor Green
Write-Host "  OTEL Logs:       http://localhost:$dashboardUiPort/structuredlogs" -ForegroundColor Green
Write-Host "  Server log:      $serverLog" -ForegroundColor Green
Write-Host ""
Write-Host "  Stop:            docker rm -f ruckr-aspire-dashboard; Stop-Job -Name RuckRServer; Remove-Job -Name RuckRServer" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Magenta
