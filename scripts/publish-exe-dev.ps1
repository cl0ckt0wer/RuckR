<#
.SYNOPSIS
    Publishes RuckR Server to the exe.dev production VM.
.DESCRIPTION
    1. Publishes framework-dependent linux-x64 build
    2. Compresses to tar.gz and SCPs to ruckr.exe.xyz:~/ruckr/releases/<timestamp>/
    3. Ensures .NET 10 runtime, Docker SQL Server, Jaeger, and a systemd service on the VM
    4. Atomically switches the current release, restarts systemd, and verifies the health endpoint
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
$absoluteDeployDir = "/home/exedev/ruckr"
$publishDir = Join-Path $repoRoot "publish"
$archiveFile = Join-Path $repoRoot "publish.tar.gz"
$serverProject = "RuckR/Server/RuckR.Server.csproj"
$releaseId = Get-Date -Format "yyyyMMddHHmmss"

function Write-Step { param($msg) Write-Host "==> $msg" -ForegroundColor Cyan }

function Escape-BashSingleQuotedValue {
    param([string]$Value)
    return "'" + $Value.Replace("'", "'\''") + "'"
}

function Get-UserSecretValue {
    param(
        [string]$Key,
        [string]$Project
    )

    $secrets = dotnet user-secrets list --project $Project 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $secrets) {
        return $null
    }

    $prefix = "$Key = "
    foreach ($line in $secrets) {
        if ($line.StartsWith($prefix, [StringComparison]::Ordinal)) {
            return $line.Substring($prefix.Length)
        }
    }

    return $null
}

function Get-PasswordFromConnectionString {
    param([string]$ConnectionString)

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        return $null
    }

    if ($ConnectionString -match '(?i)(?:^|;)\s*(?:Password|Pwd)\s*=\s*([^;]*)') {
        return $Matches[1]
    }

    return $null
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

# ── Step 4: Ensure .NET 10 runtime on VM ──
Write-Step "Checking .NET 10 runtime on $sshHost"
$dotnetInstalled = ssh $sshHost '/usr/share/dotnet/dotnet --list-runtimes 2>/dev/null | grep -q "ASP.NETCore 10\.0" && echo yes || echo no'
if ($dotnetInstalled -eq "no") {
    Write-Host "  Installing .NET 10 runtime..." -ForegroundColor Yellow
    ssh $sshHost 'wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh && chmod +x /tmp/dotnet-install.sh && sudo /tmp/dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/share/dotnet'
    Write-Host "  .NET 10 runtime installed" -ForegroundColor Green
} else {
    Write-Host "  .NET 10 runtime found" -ForegroundColor Green
}

# ── Step 5: Write secrets to remote VM ──
Write-Step "Deploying database secrets to $sshHost"
$saPassword = $env:RUCKR_DB_PASSWORD
if (-not $saPassword) {
    $saPassword = Get-UserSecretValue -Key "RUCKR_DB_PASSWORD" -Project $serverProject
}
if (-not $saPassword) {
    $devConnectionString = Get-UserSecretValue -Key "ConnectionStrings:RuckRDbContext" -Project $serverProject
    $saPassword = Get-PasswordFromConnectionString -ConnectionString $devConnectionString
}
if (-not $saPassword) {
    throw "RUCKR_DB_PASSWORD env var or user-secret must be set. Fallback also accepts user-secret ConnectionStrings:RuckRDbContext with Password=."
}

$connectionString = "Server=localhost,1433;Database=RuckR_Dev;User Id=sa;Password=$saPassword;TrustServerCertificate=True;"
$secretsContent = @"
RUCKR_DB_PASSWORD=$saPassword
MSSQL_SA_PASSWORD=$saPassword
"@
$secretsContent | ssh $sshHost "cat > ~/ruckr/secrets.env && chmod 600 ~/ruckr/secrets.env"
Write-Host "  secrets.env deployed to ~/ruckr/secrets.env (chmod 600)" -ForegroundColor Green

$appEnvContent = @"
RUCKR_DB_PASSWORD=$(Escape-BashSingleQuotedValue $saPassword)
MSSQL_SA_PASSWORD=$(Escape-BashSingleQuotedValue $saPassword)
ConnectionStrings__RuckRDbContext=$(Escape-BashSingleQuotedValue $connectionString)
OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4317
"@
$appEnvContent | ssh $sshHost "cat > ~/ruckr/app.env && chmod 600 ~/ruckr/app.env"
Write-Host "  app.env deployed to ~/ruckr/app.env (chmod 600)" -ForegroundColor Green

# ── Step 6: Ensure Docker SQL Server on VM ──
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

# ── Step 7: Ensure Jaeger on VM ──
Write-Step "Checking Jaeger on $sshHost"
$jaegerRunning = ssh $sshHost 'docker ps --filter "name=ruckr-jaeger" --format "{{.Names}}" 2>/dev/null'
if (-not $jaegerRunning) {
    Write-Host "  Starting Jaeger all-in-one container..." -ForegroundColor Yellow
    ssh $sshHost "docker rm -f ruckr-jaeger 2>/dev/null; docker run -d --name ruckr-jaeger --restart unless-stopped -p 127.0.0.1:4317:4317 -p 127.0.0.1:4318:4318 -p 127.0.0.1:16686:16686 --memory 256m -e QUERY_BASE_PATH=/jaeger jaegertracing/all-in-one:latest"
    Write-Host "  Jaeger container started (OTLP:4317, UI:/jaeger)" -ForegroundColor Green
} else {
    Write-Host "  Jaeger container already running" -ForegroundColor Green
}

# ── Step 8: Install systemd service ──
Write-Step "Installing systemd service on $sshHost"
$unitContent = @"
[Unit]
Description=RuckR Server
After=network.target docker.service
Wants=docker.service

[Service]
Type=simple
User=exedev
WorkingDirectory=$absoluteDeployDir/current
EnvironmentFile=$absoluteDeployDir/app.env
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
Environment=DOTNET_ROOT=/usr/share/dotnet
ExecStart=$absoluteDeployDir/current/RuckR.Server
Restart=always
RestartSec=5
KillSignal=SIGINT
TimeoutStopSec=30
SyslogIdentifier=ruckr

[Install]
WantedBy=multi-user.target
"@
$unitContent | ssh $sshHost "sudo tee /etc/systemd/system/ruckr.service > /dev/null && sudo systemctl daemon-reload && sudo systemctl enable ruckr.service > /dev/null"
Write-Host "  ruckr.service installed" -ForegroundColor Green

# ── Step 9: Deploy files to an immutable release directory ──
Write-Step "Deploying release $releaseId to ${sshHost}:$deployDir/releases/$releaseId"

ssh $sshHost "mkdir -p $deployDir/releases/$releaseId"

# SCP compressed archive (single-file transfer, ~30 MB vs 75 individual files)
scp $archiveFile "${sshHost}:/tmp/publish.tar.gz"

if ($LASTEXITCODE -ne 0) {
    Write-Host "SCP FAILED — check errors above" -ForegroundColor Red
    exit 1
}

# Extract on the VM, then atomically switch the current symlink.
ssh $sshHost "cd $deployDir/releases/$releaseId && tar -xzf /tmp/publish.tar.gz && rm /tmp/publish.tar.gz && chmod +x RuckR.Server && ln -sfn $absoluteDeployDir/releases/$releaseId $absoluteDeployDir/current && echo extracted"
Write-Host "  Deployed and linked current -> releases/$releaseId" -ForegroundColor Green

# Clean up local archive
Remove-Item -Force $archiveFile -ErrorAction SilentlyContinue

# ── Step 10: Restart server ──
if (-not $SkipRestart) {
    Write-Step "Restarting ruckr.service on $sshHost"
    ssh $sshHost 'for pid in $(pgrep -f "[R]uckR.Server.dll|[R]uckR\.Server" 2>/dev/null); do sudo kill "$pid"; done; sudo systemctl restart ruckr.service && sudo systemctl --no-pager --full status ruckr.service | head -30'

    Write-Host "  Server managed by systemd on port 5000 behind exe.dev proxy" -ForegroundColor Green

    # ── Step 11: Verify ──
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
                $seeded = ssh $sshHost "journalctl -u ruckr.service -n 200 --no-pager | grep -c 'Seeded' || true"
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
        Write-Host "  WARNING: Server not responding after 30s — check journalctl on $sshHost" -ForegroundColor Yellow
        $tailLog = ssh $sshHost "journalctl -u ruckr.service -n 40 --no-pager"
        Write-Host "  Last 20 log lines:" -ForegroundColor Yellow
        Write-Host $tailLog
    }

    Write-Step "Configuring exe.dev proxy"
    ssh exe.dev share port ruckr 5000
    ssh exe.dev share set-public ruckr
}

# ── Summary ──
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Magenta
Write-Host "  Published to exe.dev" -ForegroundColor Magenta
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Magenta
Write-Host "  App:  https://ruckr.exe.xyz" -ForegroundColor Green
Write-Host "  Log:  ssh $sshHost 'journalctl -u ruckr.service -f'" -ForegroundColor Green
Write-Host "  SQL:  ssh $sshHost 'docker exec ruckr-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P ... -C'" -ForegroundColor Green
Write-Host "  Publish dir: $publishDir" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Magenta
