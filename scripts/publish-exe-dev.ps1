<#
.SYNOPSIS
    Publishes RuckR Server to the exe.dev production VM.
.DESCRIPTION
    1. Publishes framework-dependent linux-x64 build
    2. Compresses to tar.gz and SCPs to ruckr.exe.xyz:~/ruckr/
    3. Ensures .NET 10 runtime and Docker SQL Server on the VM
    4. Restarts the server and verifies the health endpoint
.EXAMPLE
    .\scripts\publish-exe-dev.ps1
    .\scripts\publish-exe-dev.ps1 -SkipBuild
    .\scripts\publish-exe-dev.ps1 -SkipRestart
#>

param(
    [switch]$SkipBuild,
    [switch]$SkipRestart
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

$sshHost = "ruckr.exe.xyz"
$deployDir = "~/ruckr"
$publishDir = Join-Path $repoRoot "publish"
$archiveFile = Join-Path $repoRoot "publish.tar.gz"
$serverProject = "RuckR/Server/RuckR.Server.csproj"

function Write-Step { param($msg) Write-Host "==> $msg" -ForegroundColor Cyan }

function Escape-BashSingleQuotedValue {
    param([string]$Value)
    return "'" + $Value.Replace("'", "'\''") + "'"
}

# ── Step 1: Publish ──
if (-not $SkipBuild) {
    Write-Step "Publishing framework-dependent linux-x64 build to $publishDir"
    Push-Location $repoRoot

    if (Test-Path $publishDir) {
        Remove-Item -Recurse -Force $publishDir
    }

    dotnet publish $serverProject `
        -c Release `
        -r linux-x64 `
        --no-self-contained `
        -o $publishDir

    if ($LASTEXITCODE -ne 0) {
        Write-Host "PUBLISH FAILED — check errors above" -ForegroundColor Red
        Pop-Location
        exit 1
    }

    Write-Host "  Published: $publishDir" -ForegroundColor Green
    Pop-Location
} else {
    Write-Step "Skipping publish (SkipBuild flag set)"
}

# ── Step 2: Check SSH connectivity ──
Write-Step "Checking SSH connectivity to $sshHost"
$sshOk = $false
try {
    ssh -o ConnectTimeout=5 -o BatchMode=yes $sshHost "echo ok" 2>$null
    if ($LASTEXITCODE -eq 0) {
        $sshOk = $true
        Write-Host "  Connected" -ForegroundColor Green
    }
} catch {}

if (-not $sshOk) {
    Write-Host "  SSH connection failed. Trying with password..." -ForegroundColor Yellow
    ssh -o ConnectTimeout=5 $sshHost "echo ok"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "SSH CONNECTION FAILED — check connectivity to $sshHost" -ForegroundColor Red
        exit 1
    }
}

# ── Step 3: Compress publish output ──
Write-Step "Compressing publish output to $archiveFile"
Push-Location $repoRoot

if (Test-Path $archiveFile) {
    Remove-Item -Force $archiveFile
}

# tar is available on Linux/WSL/macOS; fall back to Compress-Archive on Windows
$tarAvailable = Get-Command tar -ErrorAction SilentlyContinue
if ($tarAvailable) {
    tar -czf $archiveFile -C publish .
} else {
    Compress-Archive -Path "$publishDir\*" -DestinationPath (Join-Path $repoRoot "publish.zip")
    $archiveFile = Join-Path $repoRoot "publish.zip"
}

$archiveSize = (Get-Item $archiveFile).Length / 1MB
Write-Host "  Compressed: $([math]::Round($archiveSize, 1)) MB" -ForegroundColor Green
Pop-Location

# ── Step 4: Stop the running server on the VM ──
Write-Step "Stopping existing server on $sshHost"
ssh $sshHost 'for pid in $(pgrep -f "[R]uckR.Server.dll|[R]uckR\.Server" 2>/dev/null); do sudo kill "$pid"; done; echo "done"'
Start-Sleep -Seconds 2

# ── Step 5: Ensure .NET 10 runtime on VM ──
Write-Step "Checking .NET 10 runtime on $sshHost"
$dotnetInstalled = ssh $sshHost '/usr/share/dotnet/dotnet --list-runtimes 2>/dev/null | grep -q "ASP.NETCore 10\.0" && echo yes || echo no'
if ($dotnetInstalled -eq "no") {
    Write-Host "  Installing .NET 10 runtime..." -ForegroundColor Yellow
    ssh $sshHost 'wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh && chmod +x /tmp/dotnet-install.sh && sudo /tmp/dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/share/dotnet'
    Write-Host "  .NET 10 runtime installed" -ForegroundColor Green
} else {
    Write-Host "  .NET 10 runtime found" -ForegroundColor Green
}

# ── Step 6: Write secrets to remote VM ──
Write-Step "Deploying database secrets to $sshHost"
$saPassword = if ($env:RUCKR_DB_PASSWORD) { $env:RUCKR_DB_PASSWORD } else { throw "RUCKR_DB_PASSWORD environment variable must be set" }

$connectionString = "Server=localhost,1433;Database=RuckR_Dev;User Id=sa;Password=$saPassword;TrustServerCertificate=True;"
$secretsContent = @"
RUCKR_DB_PASSWORD=$saPassword
MSSQL_SA_PASSWORD=$saPassword
"@
$secretsContent | ssh $sshHost "cat > ~/ruckr/secrets.env && chmod 600 ~/ruckr/secrets.env"
Write-Host "  secrets.env deployed to ~/ruckr/secrets.env (chmod 600)" -ForegroundColor Green

$appEnvContent = @"
export RUCKR_DB_PASSWORD=$(Escape-BashSingleQuotedValue $saPassword)
export MSSQL_SA_PASSWORD=$(Escape-BashSingleQuotedValue $saPassword)
export ConnectionStrings__RuckRDbContext=$(Escape-BashSingleQuotedValue $connectionString)
"@
$appEnvContent | ssh $sshHost "cat > ~/ruckr/app.env && chmod 600 ~/ruckr/app.env"
Write-Host "  app.env deployed to ~/ruckr/app.env (chmod 600)" -ForegroundColor Green

# ── Step 6.5: Ensure Docker SQL Server on VM ──
Write-Step "Checking Docker SQL Server on $sshHost"
$sqlRunning = ssh $sshHost 'docker ps --filter "name=ruckr-sql" --format "{{.Names}}" 2>/dev/null'
if (-not $sqlRunning) {
    Write-Host "  Starting SQL Server container..." -ForegroundColor Yellow
    ssh $sshHost "docker rm -f ruckr-sql 2>/dev/null; docker run -d --name ruckr-sql --restart unless-stopped --env-file ~/ruckr/secrets.env -e ACCEPT_EULA=Y -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest"
    Write-Host "  Waiting for SQL Server to be ready..." -ForegroundColor Yellow
    for ($i = 0; $i -lt 20; $i++) {
        $ready = ssh $sshHost 'docker logs ruckr-sql 2>&1 | grep -q "SQL Server is now ready" && echo ready || echo waiting'
        if ($ready -eq "ready") { break }
        Start-Sleep -Seconds 3
    }
    Write-Host "  SQL Server container started" -ForegroundColor Green
} else {
    Write-Host "  SQL Server container already running" -ForegroundColor Green
}

# ── Step 7: Deploy files to VM ──
Write-Step "Deploying files to ${sshHost}:$deployDir"

# Create deploy directory and clean old files while preserving secrets.env.
ssh $sshHost "mkdir -p $deployDir && find $deployDir -mindepth 1 ! -name secrets.env ! -name app.env -exec rm -rf {} +"

# SCP compressed archive (single-file transfer, ~30 MB vs 75 individual files)
scp $archiveFile "${sshHost}:/tmp/publish.tar.gz"

if ($LASTEXITCODE -ne 0) {
    Write-Host "SCP FAILED — check errors above" -ForegroundColor Red
    exit 1
}

# Extract on the VM
ssh $sshHost "cd $deployDir && tar -xzf /tmp/publish.tar.gz && rm /tmp/publish.tar.gz && echo extracted"
Write-Host "  Deployed and extracted" -ForegroundColor Green

# Clean up local archive
Remove-Item -Force $archiveFile -ErrorAction SilentlyContinue

# ── Step 8: Restart server ──
if (-not $SkipRestart) {
    Write-Step "Starting server on $sshHost"
    # Launch the server via a detached nohup job that survives SSH exit.
    # The outer nohup redirects stdio; the inner exec replaces the bash process.
    ssh $sshHost "nohup bash -c '. ~/ruckr/app.env && cd $deployDir && exec env DOTNET_ROOT=/usr/share/dotnet ASPNETCORE_URLS=''http://127.0.0.1:5000'' ASPNETCORE_ENVIRONMENT=Production ./RuckR.Server' >/tmp/ruckr.log 2>&1 & disown; echo started"

    Write-Host "  Server started on port 5000 behind nginx (migrations apply on startup)" -ForegroundColor Green

    # ── Step 9: Verify ──
    Write-Step "Verifying server is reachable..."
    $verifyOk = $false
    for ($i = 0; $i -lt 15; $i++) {
        try {
            $response = Invoke-WebRequest -Uri "https://ruckr.exe.xyz/api/telemetry/health" `
                -TimeoutSec 5 -ErrorAction Stop
            if ($response.StatusCode -eq 200) {
                $verifyOk = $true
                Write-Host "  Server is healthy!" -ForegroundColor Green

                # Quick check if migrations succeeded
                $seeded = ssh $sshHost "grep -c 'Seeded' /tmp/ruckr.log 2>/dev/null || echo 0"
                if ($seeded -gt 0) {
                    Write-Host "  DB migrations applied, seed data generated" -ForegroundColor Green
                } else {
                    Write-Host "  Migrations may still be in progress (check logs)" -ForegroundColor Yellow
                }
                break
            }
        } catch {
            Start-Sleep -Seconds 2
        }
    }

    if (-not $verifyOk) {
        Write-Host "  WARNING: Server not responding after 30s — check /tmp/ruckr.log on $sshHost" -ForegroundColor Yellow
        $tailLog = ssh $sshHost "tail -20 /tmp/ruckr.log"
        Write-Host "  Last 20 log lines:" -ForegroundColor Yellow
        Write-Host $tailLog
    }
}

# ── Summary ──
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Magenta
Write-Host "  Published to exe.dev" -ForegroundColor Magenta
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Magenta
Write-Host "  App:  https://ruckr.exe.xyz" -ForegroundColor Green
Write-Host "  Log:  ssh $sshHost 'tail /tmp/ruckr.log'" -ForegroundColor Green
Write-Host "  SQL:  ssh $sshHost 'docker exec ruckr-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P ... -C'" -ForegroundColor Green
Write-Host "  Publish dir: $publishDir" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Magenta
