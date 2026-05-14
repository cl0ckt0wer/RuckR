#!/usr/bin/env bash
# v2 — Native bash, zero manual steps. One command to publish, deploy, and verify.
# Usage: ./scripts/publish-exe-dev.sh [--skip-build] [--skip-restart] [--yes]
set -euo pipefail

SSH_HOST="ruckr.exe.xyz"
DEPLOY_DIR="~/ruckr"
ABS_DEPLOY_DIR="/home/exedev/ruckr"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUBLISH_DIR="$REPO_ROOT/publish"
ARCHIVE="$REPO_ROOT/publish.tar.gz"
SERVER_CSPROJ="$REPO_ROOT/RuckR/Server/RuckR.Server.csproj"
LOCAL_BACKUP_DIR="${RUCKR_LOCAL_BACKUP_DIR:-/mnt/c/Users/clock/dbbackups}"
RELEASE_ID=$(date +%Y%m%d%H%M%S)
PREV_RELEASE=""

GREEN='\033[0;32m'; CYAN='\033[0;36m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; MAGENTA='\033[0;35m'; BOLD='\033[1m'; NC='\033[0m'

SKIP_BUILD=false; SKIP_RESTART=false; NONINTERACTIVE=false
for arg in "$@"; do case "$arg" in --skip-build) SKIP_BUILD=true ;; --skip-restart) SKIP_RESTART=true ;; --yes|-y) NONINTERACTIVE=true ;; *) echo "Unknown: $arg"; exit 1 ;; esac; done

step()   { echo -e "\n${CYAN}━━━ ${BOLD}$1${NC}"; }
info()   { echo -e "  ${BOLD}$1${NC} $2"; }
ok()     { echo -e "  ${GREEN}✓${NC} $1"; }
warn()   { echo -e "  ${YELLOW}⚠${NC} $1"; }
fail()   { echo -e "  ${RED}✗${NC} $1"; exit 1; }
run()    { echo -e "  \$${NC} $*"; "$@"; }
confirm(){
  local msg=$1
  if $NONINTERACTIVE; then return 0; fi
  read -r -p "  ${YELLOW}?${NC} $msg [Y/n] " reply
  [[ -z "$reply" || "$reply" =~ ^[Yy] ]] && return 0 || return 1
}

get_windows_env(){
  local name=$1
  local powershell_path="/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe"
  [ -x "$powershell_path" ] || return 0

  "$powershell_path" -NoProfile -Command \
    "\$value = [Environment]::GetEnvironmentVariable('$name', 'User'); if (-not \$value) { \$value = [Environment]::GetEnvironmentVariable('$name', 'Machine') }; [Console]::Out.Write(\$value)" \
    2>/dev/null || true
}

resolve_first_env(){
  local value=""
  local name
  for name in "$@"; do
    value="${!name:-}"
    if [ -n "$value" ]; then
      printf '%s' "$value"
      return 0
    fi
  done

  for name in "$@"; do
    value=$(get_windows_env "$name")
    if [ -n "$value" ]; then
      printf '%s' "$value"
      return 0
    fi
  done
}

cleanup() { rm -f "$ARCHIVE"; rm -rf "$PUBLISH_DIR"; }
trap cleanup EXIT

# ── Resolve DB password ──
step "1/10  Resolving DB password"
PASSWORD="${RUCKR_DB_PASSWORD:-}"
if [ -z "$PASSWORD" ]; then
  secrets=$(dotnet user-secrets list --project "$SERVER_CSPROJ" 2>/dev/null || true)
  conn=$(echo "$secrets" | sed -n 's/^ConnectionStrings:RuckRDbContext = //p')
  if [ -n "$conn" ]; then
    PASSWORD=$(echo "$conn" | sed -n 's/.*[Pp][Aa][Ss][Ss][Ww][Oo][Rr][Dd]=\([^;]*\).*/\1/p')
  fi
fi
[ -n "$PASSWORD" ] || fail "No DB password found. Set RUCKR_DB_PASSWORD env var or user-secret ConnectionStrings:RuckRDbContext."
ok "Password resolved"

CONNECTION_STRING="Server=localhost,1433;Database=RuckR_Dev;User Id=sa;Password=${PASSWORD};TrustServerCertificate=True;"

# ── Pre-flight checks ──
step "2/10  Pre-flight checks"
for bin in dotnet ssh scp tar curl; do
  command -v "$bin" >/dev/null || fail "$bin not found on PATH"
done
ok "All tools available (dotnet, ssh, scp, tar, curl)"
dotnet --version | xargs -I{} echo "  .NET SDK: {}"
ssh -o ConnectTimeout=5 -o BatchMode=yes "$SSH_HOST" "echo ok" 2>/dev/null && ok "SSH to $SSH_HOST" || fail "SSH to $SSH_HOST failed. Check connectivity and credentials."

# ── Publish ──
step "3/10  Building framework-dependent linux-x64"
if $SKIP_BUILD; then
  warn "Skipping build (--skip-build)"
else
  rm -rf "$PUBLISH_DIR"
  run dotnet publish "$SERVER_CSPROJ" -c Release -r linux-x64 --no-self-contained -o "$PUBLISH_DIR"
  size=$(du -sh "$PUBLISH_DIR" | cut -f1)
  ok "Published ($size)"
fi

# ── Compress ──
step "4/10  Compressing artifacts"
rm -f "$ARCHIVE"
tar -czf "$ARCHIVE" -C "$PUBLISH_DIR" .
archive_mb=$(du -h "$ARCHIVE" | cut -f1)
ok "Compressed ($archive_mb)"

# ── Ensure VM infra (runtime, SQL, Jaeger) ──
step "5/10  Checking VM infrastructure"
runtime=$(ssh "$SSH_HOST" '/usr/share/dotnet/dotnet --list-runtimes 2>/dev/null | grep -c "ASP.NETCore 10\.0" || true')
if [ "$runtime" -eq 0 ]; then
  info "Installing" ".NET 10 runtime..."
  ssh "$SSH_HOST" 'wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh && chmod +x /tmp/dotnet-install.sh && sudo /tmp/dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/share/dotnet'
  ok ".NET 10 runtime installed"
else
  ok ".NET 10 runtime present"
fi

sql=$(ssh "$SSH_HOST" 'docker ps --filter "name=ruckr-sql" --format "{{.Names}}" 2>/dev/null')
if [ -z "$sql" ]; then
  info "Starting" "SQL Server container..."
  # Data persisted to host volume — survives container recreation
  ssh "$SSH_HOST" "docker rm -f ruckr-sql 2>/dev/null; docker run -d --name ruckr-sql --restart unless-stopped -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=$PASSWORD -v /var/lib/ruckr/mssql:/var/opt/mssql -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest"
  for i in $(seq 1 20); do
    ready=$(ssh "$SSH_HOST" 'docker logs ruckr-sql 2>&1 | grep -q "SQL Server is now ready" && echo ready || echo waiting')
    [ "$ready" = "ready" ] && break
    sleep 3
  done
  ok "SQL Server ready"
else
  ok "SQL Server container running"
fi

jaeger=$(ssh "$SSH_HOST" 'docker ps --filter "name=ruckr-jaeger" --format "{{.Names}}" 2>/dev/null')
if [ -z "$jaeger" ]; then
  info "Starting" "Jaeger container..."
  ssh "$SSH_HOST" "docker rm -f ruckr-jaeger 2>/dev/null; docker run -d --name ruckr-jaeger --restart unless-stopped -p 127.0.0.1:4317:4317 -p 127.0.0.1:4318:4318 -p 127.0.0.1:16686:16686 --memory 256m -e QUERY_BASE_PATH=/jaeger jaegertracing/all-in-one:latest"
  ok "Jaeger started"
else
  ok "Jaeger container running"
fi

# ── Deploy secrets ──
step "6/10  Deploying secrets"
ssh "$SSH_HOST" "cat > $DEPLOY_DIR/secrets.env" <<SECEOF
RUCKR_DB_PASSWORD=$PASSWORD
MSSQL_SA_PASSWORD=$PASSWORD
SECEOF
ssh "$SSH_HOST" "chmod 600 $DEPLOY_DIR/secrets.env"

esc_pass=$(printf '%s\n' "$PASSWORD" | sed "s/'/'\\\\''/g")
esc_conn=$(printf '%s\n' "$CONNECTION_STRING" | sed "s/'/'\\\\''/g")

# Resolve map keys (optional — only set if env vars exist)
ARC_GIS_KEY=$(resolve_first_env ARC_GIS_API_KEY ArcGISApiKey)
ARC_GIS_PORTAL_ITEM_ID=$(resolve_first_env ARC_GIS_PORTAL_ITEM_ID ArcGISPortalItemId)
GEOBLAZOR_LICENSE_KEY=$(resolve_first_env GEOBLAZOR_API GEOBLAZOR_LICENSE_KEY GEOBLAZOR_REGISTRATION_KEY GeoBlazor__LicenseKey GeoBlazor__RegistrationKey)
if [ -n "$ARC_GIS_KEY" ]; then
  ok "ArcGIS API key resolved (length: ${#ARC_GIS_KEY})"
else
  warn "ArcGIS API key not set; map will report missing ArcGIS key"
fi
if [ -n "$ARC_GIS_PORTAL_ITEM_ID" ]; then
  ok "ArcGIS Portal Item ID resolved (length: ${#ARC_GIS_PORTAL_ITEM_ID})"
else
  warn "ArcGIS Portal Item ID not set; map will report missing Portal Item ID"
fi
if [ -n "$GEOBLAZOR_LICENSE_KEY" ]; then
  ok "GeoBlazor license key resolved (length: ${#GEOBLAZOR_LICENSE_KEY})"
else
  warn "GeoBlazor license key not set; map will report missing GeoBlazor key"
fi
esc_geoblazor=$(printf '%s\n' "$GEOBLAZOR_LICENSE_KEY" | sed "s/'/'\\\\''/g")

ssh "$SSH_HOST" "cat > $DEPLOY_DIR/app.env" <<APPEOF
RUCKR_DB_PASSWORD='$esc_pass'
MSSQL_SA_PASSWORD='$esc_pass'
ConnectionStrings__RuckRDbContext='$esc_conn'
OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4317
ArcGISApiKey='$ARC_GIS_KEY'
ArcGISPortalItemId='$ARC_GIS_PORTAL_ITEM_ID'
GeoBlazor__LicenseKey='$esc_geoblazor'
APPEOF
ssh "$SSH_HOST" "chmod 600 $DEPLOY_DIR/app.env"
ok "secrets.env and app.env deployed (chmod 600)"

# ── DB backup before restart ──
step "6b/10  Backing up database"
backup_output=""
if backup_output=$(ssh "$SSH_HOST" "bash -se" <<'SSHEOF'
set -euo pipefail
set -a
. /home/exedev/ruckr/app.env
set +a

backup_dir=/var/opt/mssql/backup
backup_file="$backup_dir/ruckr_$(date +%Y%m%d_%H%M%S).bak"
backup_name=$(basename "$backup_file")
staged_backup_file="/tmp/$backup_name"
docker exec ruckr-sql mkdir -p "$backup_dir"
docker exec -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" ruckr-sql \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C \
  -Q "BACKUP DATABASE [RuckR_Dev] TO DISK = N'${backup_file}' WITH INIT, COMPRESSION;"
docker cp "ruckr-sql:$backup_file" "$staged_backup_file"
chmod 600 "$staged_backup_file"
echo "BACKUP_FILE=$backup_file"
echo "STAGED_BACKUP_FILE=$staged_backup_file"
SSHEOF
); then
  remote_backup_file=$(printf '%s\n' "$backup_output" | sed -n 's/^BACKUP_FILE=//p' | tail -1)
  staged_backup_file=$(printf '%s\n' "$backup_output" | sed -n 's/^STAGED_BACKUP_FILE=//p' | tail -1)
  ok "DB backup created"
  if [ -n "$staged_backup_file" ]; then
    mkdir -p "$LOCAL_BACKUP_DIR"
    if scp "$SSH_HOST:$staged_backup_file" "$LOCAL_BACKUP_DIR/"; then
      ok "DB backup copied to $LOCAL_BACKUP_DIR/$(basename "$staged_backup_file")"
      ssh "$SSH_HOST" "rm -f '$staged_backup_file'" >/dev/null 2>&1 || true
    else
      warn "DB backup copy to $LOCAL_BACKUP_DIR failed (non-fatal)"
    fi
  else
    warn "DB backup path not returned; local copy skipped (non-fatal)"
  fi
else
  warn "DB backup failed (non-fatal)"
fi

# ── Install systemd service ──
step "7/10  Installing systemd service"
cat <<UNIT | ssh "$SSH_HOST" "sudo tee /etc/systemd/system/ruckr.service > /dev/null && sudo systemctl daemon-reload && sudo systemctl enable ruckr.service > /dev/null"
[Unit]
Description=RuckR Server
After=network.target docker.service
Wants=docker.service

[Service]
Type=simple
User=exedev
WorkingDirectory=$ABS_DEPLOY_DIR/current
EnvironmentFile=$ABS_DEPLOY_DIR/app.env
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
Environment=DOTNET_ROOT=/usr/share/dotnet
ExecStart=$ABS_DEPLOY_DIR/current/RuckR.Server
Restart=always
RestartSec=5
KillSignal=SIGINT
TimeoutStopSec=30
SyslogIdentifier=ruckr

[Install]
WantedBy=multi-user.target
UNIT
ok "ruckr.service installed and enabled"

# ── Capture current symlink for rollback ──
step "8/10  Deploying release $RELEASE_ID"
PREV_RELEASE=$(ssh "$SSH_HOST" "readlink -f $ABS_DEPLOY_DIR/current 2>/dev/null || true")

ssh "$SSH_HOST" "mkdir -p $DEPLOY_DIR/releases/$RELEASE_ID"
run scp "$ARCHIVE" "$SSH_HOST:/tmp/publish.tar.gz"
ssh "$SSH_HOST" "cd $DEPLOY_DIR/releases/$RELEASE_ID && tar -xzf /tmp/publish.tar.gz && rm /tmp/publish.tar.gz && chmod +x RuckR.Server && ln -sfn $ABS_DEPLOY_DIR/releases/$RELEASE_ID $ABS_DEPLOY_DIR/current && echo 'extracted and linked'"
ok "Deployed current → releases/$RELEASE_ID"

# ── Restart & verify ──
step "9/10  Restarting server"
if $SKIP_RESTART; then
  warn "Skipping restart (--skip-restart)"
else
  ssh "$SSH_HOST" "sudo systemctl restart ruckr.service"
  sleep 2
  status=$(ssh "$SSH_HOST" "sudo systemctl is-active ruckr.service 2>/dev/null || true")
  if [ "$status" != "active" ]; then
    warn "systemd restart failed. Rolling back to previous release..."
    if [ -n "$PREV_RELEASE" ]; then
      ssh "$SSH_HOST" "ln -sfn $PREV_RELEASE $ABS_DEPLOY_DIR/current && sudo systemctl restart ruckr.service"
      warn "Rolled back to $PREV_RELEASE"
    fi
    ssh "$SSH_HOST" "sudo systemctl --no-pager status ruckr.service | tail -20"
    fail "Restart failed"
  fi
  ok "systemd restarted (active)"

# ── Verify health endpoint ──
  step "10/10  Verifying health endpoint"
  healthy=false
  for i in $(seq 1 15); do
    code=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 5 "https://ruckr.exe.xyz/api/telemetry/health" 2>/dev/null || true)
    if [ "$code" = "200" ]; then
      healthy=true
      ok "Server healthy at https://ruckr.exe.xyz/api/telemetry/health"
      seeded=$(ssh "$SSH_HOST" 'journalctl -u ruckr.service -n 200 --no-pager 2>/dev/null | grep -c "Seeded" || true')
      if [ "$seeded" -gt 0 ]; then
        ok "DB migrations applied, seed data generated"
      else
        warn "No 'Seeded' log entry found — migrations may still be running"
      fi
      break
    fi
    sleep 2
  done

  if ! $healthy; then
    warn "Health check failed after 30s. Logs:"
    ssh "$SSH_HOST" "journalctl -u ruckr.service -n 40 --no-pager 2>/dev/null || true"
  fi

  info "Configuring" "exe.dev proxy..."
  ssh exe.dev share port ruckr 5000 2>/dev/null || true
  ssh exe.dev share set-public ruckr 2>/dev/null || true
fi

# ── Summary ──
echo ""
echo -e "${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}  Published to exe.dev${NC}"
echo -e "${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "  ${BOLD}App:${NC}  https://ruckr.exe.xyz"
echo -e "  ${BOLD}Release:${NC}  $RELEASE_ID"
echo -e "  ${BOLD}Log:${NC}  ssh $SSH_HOST 'journalctl -u ruckr.service -f'"
echo -e "  ${BOLD}SQL:${NC}  ssh $SSH_HOST 'docker exec ruckr-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C'"
echo -e "${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
