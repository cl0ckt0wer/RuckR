#!/usr/bin/env bash
# Native bash deploy. Builds on the exe.dev VM from the current pushed Git commit,
# then atomically switches ~/ruckr/current and verifies health.
# Usage: ./scripts/publish-exe-dev.sh [--app-only] [--no-restore] [--skip-restart] [--yes] [--ref <git-ref>]
set -euo pipefail

SSH_HOST="ruckr.exe.xyz"
DEPLOY_DIR="~/ruckr"
ABS_DEPLOY_DIR="/home/exedev/ruckr"
REMOTE_REPO_DIR="$ABS_DEPLOY_DIR/src"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SERVER_CSPROJ_REL="RuckR/Server/RuckR.Server.csproj"
SERVER_CSPROJ="$REPO_ROOT/$SERVER_CSPROJ_REL"
LOCAL_BACKUP_DIR="${RUCKR_LOCAL_BACKUP_DIR:-/mnt/c/Users/clock/dbbackups}"
RELEASE_ID=$(date +%Y%m%d%H%M%S)
PREV_RELEASE=""
RELEASES_TO_KEEP=10
DEPLOY_REF="${RUCKR_DEPLOY_REF:-}"

GREEN='\033[0;32m'; CYAN='\033[0;36m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; MAGENTA='\033[0;35m'; BOLD='\033[1m'; NC='\033[0m'

APP_ONLY=false; NO_RESTORE=false; SKIP_RESTART=false; NONINTERACTIVE=false
while [ "$#" -gt 0 ]; do
  case "$1" in
    --app-only) APP_ONLY=true; shift ;;
    --no-restore) NO_RESTORE=true; shift ;;
    --skip-restart) SKIP_RESTART=true; shift ;;
    --yes|-y) NONINTERACTIVE=true; shift ;;
    --ref) DEPLOY_REF="${2:-}"; [ -n "$DEPLOY_REF" ] || { echo "--ref requires a value"; exit 1; }; shift 2 ;;
    --skip-build) echo "Unknown: --skip-build (remote-build deploy always publishes a fresh release)"; exit 1 ;;
    *) echo "Unknown: $1"; exit 1 ;;
  esac
done

step()   { echo -e "\n${CYAN}━━━ ${BOLD}$1${NC}"; }
info()   { echo -e "  ${BOLD}$1${NC} $2"; }
ok()     { echo -e "  ${GREEN}✓${NC} $1"; }
warn()   { echo -e "  ${YELLOW}⚠${NC} $1"; }
fail()   { echo -e "  ${RED}✗${NC} $1" >&2; exit 1; }

get_windows_env(){
  local name=$1
  local powershell_path=""

  for candidate in \
    "powershell.exe" \
    "pwsh.exe" \
    "/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe" \
    "/mnt/c/Program Files/PowerShell/7/pwsh.exe"; do
    if command -v "$candidate" >/dev/null 2>&1 || [ -x "$candidate" ]; then
      powershell_path="$candidate"
      break
    fi
  done

  [ -n "$powershell_path" ] || return 0

  "$powershell_path" -NoProfile -Command \
    "\$value = [Environment]::GetEnvironmentVariable('$name', 'Process'); if (-not \$value) { \$value = [Environment]::GetEnvironmentVariable('$name', 'User') }; if (-not \$value) { \$value = [Environment]::GetEnvironmentVariable('$name', 'Machine') }; [Console]::Out.Write(\$value)" \
    2>/dev/null | tr -d '\r' || true
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

remote_quote(){
  printf "'%s'" "$(printf '%s' "$1" | sed "s/'/'\\\\''/g")"
}

local_git_clean_check(){
  if [ -n "$(git -C "$REPO_ROOT" status --porcelain --untracked-files=no)" ] && [ -z "${RUCKR_DEPLOY_ALLOW_DIRTY:-}" ]; then
    fail "Working tree has tracked uncommitted changes. Commit and push first, or deploy a specific pushed --ref."
  fi
}

resolve_deploy_ref(){
  if [ -n "$DEPLOY_REF" ]; then
    git -C "$REPO_ROOT" rev-parse --verify "$DEPLOY_REF^{commit}" >/dev/null
    git -C "$REPO_ROOT" rev-parse "$DEPLOY_REF"
    return 0
  else
    DEPLOY_REF=$(git -C "$REPO_ROOT" rev-parse HEAD)
    local_git_clean_check
  fi

  local branch ahead
  branch=$(git -C "$REPO_ROOT" branch --show-current)
  if [ -n "$branch" ]; then
    ahead=$(git -C "$REPO_ROOT" rev-list --count "origin/$branch..$branch" 2>/dev/null || echo 0)
    if [ "$ahead" != "0" ] && [ -z "${RUCKR_DEPLOY_ALLOW_UNPUSHED:-}" ]; then
      fail "Current branch has $ahead unpushed commit(s). Push before deploying so the VM can fetch the ref."
    fi
  fi

  git -C "$REPO_ROOT" rev-parse "$DEPLOY_REF"
}

# ── Resolve DB password ──
step "1/9  Resolving DB password"
PASSWORD=$(resolve_first_env RUCKR_DB_PASSWORD)
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
step "2/9  Pre-flight checks"
for bin in dotnet git ssh scp curl; do
  command -v "$bin" >/dev/null || fail "$bin not found on PATH"
done
ok "Local tools available (dotnet, git, ssh, scp, curl)"
ssh -o ConnectTimeout=5 -o BatchMode=yes "$SSH_HOST" "echo ok" 2>/dev/null && ok "SSH to $SSH_HOST" || fail "SSH to $SSH_HOST failed. Check connectivity and credentials."

GIT_REMOTE_URL=$(git -C "$REPO_ROOT" config --get remote.origin.url)
[ -n "$GIT_REMOTE_URL" ] || fail "No origin remote configured."
GIT_COMMIT=$(resolve_deploy_ref)
ok "Deploy ref resolved to $GIT_COMMIT"

# ── Ensure VM infra (SDK/runtime, SQL, Jaeger) ──
step "3/9  Checking VM infrastructure"
if $APP_ONLY; then
  warn "Skipping VM runtime/container checks (--app-only)"
else
  sdk=$(ssh "$SSH_HOST" '/usr/share/dotnet/dotnet --list-sdks 2>/dev/null | grep -c "^10\." || true')
  if [ "$sdk" -eq 0 ]; then
    info "Installing" ".NET 10 SDK..."
    ssh "$SSH_HOST" 'wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh && chmod +x /tmp/dotnet-install.sh && sudo /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet'
    ok ".NET 10 SDK installed"
  else
    ok ".NET 10 SDK present"
  fi

  sql=$(ssh "$SSH_HOST" 'docker ps --filter "name=ruckr-sql" --format "{{.Names}}" 2>/dev/null')
  if [ -z "$sql" ]; then
    info "Starting" "SQL Server container..."
    ssh "$SSH_HOST" "docker rm -f ruckr-sql 2>/dev/null; docker run -d --name ruckr-sql --restart unless-stopped -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=$(remote_quote "$PASSWORD") -v /var/lib/ruckr/mssql:/var/opt/mssql -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest"
    for _ in $(seq 1 20); do
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
fi

# ── Deploy secrets ──
step "4/9  Deploying secrets"
ssh "$SSH_HOST" "mkdir -p $DEPLOY_DIR && cat > $DEPLOY_DIR/secrets.env" <<SECEOF
RUCKR_DB_PASSWORD=$PASSWORD
MSSQL_SA_PASSWORD=$PASSWORD
SECEOF
ssh "$SSH_HOST" "chmod 600 $DEPLOY_DIR/secrets.env"

ARC_GIS_KEY=$(resolve_first_env ARC_GIS_API_KEY ArcGISApiKey)
ARC_GIS_PORTAL_ITEM_ID=$(resolve_first_env ARC_GIS_PORTAL_ITEM_ID ArcGISPortalItemId)
GEOBLAZOR_LICENSE_KEY=$(resolve_first_env GEOBLAZOR_API GEOBLAZOR_LICENSE_KEY GEOBLAZOR_REGISTRATION_KEY GeoBlazor__LicenseKey GeoBlazor__RegistrationKey)

[ -n "$ARC_GIS_KEY" ] && ok "ArcGIS API key resolved (length: ${#ARC_GIS_KEY})" || warn "ArcGIS API key not set; map will report missing ArcGIS key"
[ -n "$ARC_GIS_PORTAL_ITEM_ID" ] && ok "ArcGIS Portal Item ID resolved (length: ${#ARC_GIS_PORTAL_ITEM_ID})" || warn "ArcGIS Portal Item ID not set; map will report missing Portal Item ID"
[ -n "$GEOBLAZOR_LICENSE_KEY" ] && ok "GeoBlazor license key resolved (length: ${#GEOBLAZOR_LICENSE_KEY})" || warn "GeoBlazor license key not set; map will report missing GeoBlazor key"

ssh "$SSH_HOST" "cat > $DEPLOY_DIR/app.env" <<APPEOF
RUCKR_DB_PASSWORD=$(remote_quote "$PASSWORD")
MSSQL_SA_PASSWORD=$(remote_quote "$PASSWORD")
ConnectionStrings__RuckRDbContext=$(remote_quote "$CONNECTION_STRING")
OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4317
ArcGISApiKey=$(remote_quote "$ARC_GIS_KEY")
ArcGISPortalItemId=$(remote_quote "$ARC_GIS_PORTAL_ITEM_ID")
GeoBlazor__LicenseKey=$(remote_quote "$GEOBLAZOR_LICENSE_KEY")
GeoBlazor__RegistrationKey=$(remote_quote "$GEOBLAZOR_LICENSE_KEY")
APPEOF
ssh "$SSH_HOST" "chmod 600 $DEPLOY_DIR/app.env"
ok "secrets.env and app.env deployed (chmod 600)"

# ── DB backup before restart ──
step "5/9  Backing up database"
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
  fi
else
  warn "DB backup failed (non-fatal)"
fi

# ── Install systemd service ──
step "6/9  Installing systemd service"
if $APP_ONLY; then
  warn "Skipping systemd service install (--app-only)"
else
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
ExecStart=/usr/share/dotnet/dotnet $ABS_DEPLOY_DIR/current/RuckR.Server.dll
Restart=always
RestartSec=5
KillSignal=SIGINT
TimeoutStopSec=30
SyslogIdentifier=ruckr

[Install]
WantedBy=multi-user.target
UNIT
ok "ruckr.service installed and enabled"
fi

# ── Remote checkout and publish ──
step "7/9  Building release $RELEASE_ID on VM"
PREV_RELEASE=$(ssh "$SSH_HOST" "readlink -f $ABS_DEPLOY_DIR/current 2>/dev/null || true")
remote_url_q=$(remote_quote "$GIT_REMOTE_URL")
commit_q=$(remote_quote "$GIT_COMMIT")
release_q=$(remote_quote "$ABS_DEPLOY_DIR/releases/$RELEASE_ID")
repo_q=$(remote_quote "$REMOTE_REPO_DIR")
no_restore_arg=""
$NO_RESTORE && no_restore_arg="--no-restore"

ssh "$SSH_HOST" "bash -se" <<SSHEOF
set -euo pipefail
repo=$repo_q
release_dir=$release_q
remote_url=$remote_url_q
commit=$commit_q

mkdir -p "$ABS_DEPLOY_DIR/releases"
if [ -d "\$repo/.git" ]; then
  git -C "\$repo" remote set-url origin "\$remote_url"
  git -C "\$repo" fetch --prune origin
else
  rm -rf "\$repo"
  git clone "\$remote_url" "\$repo"
fi

git -C "\$repo" fetch --prune origin
git -C "\$repo" checkout --detach "\$commit"
rm -rf "\$release_dir"
mkdir -p "\$release_dir"
/usr/share/dotnet/dotnet publish "\$repo/$SERVER_CSPROJ_REL" -c Release -o "\$release_dir" $no_restore_arg
ln -sfn "\$release_dir" "$ABS_DEPLOY_DIR/current"
SSHEOF
ok "Built and linked current → releases/$RELEASE_ID"

step "7b/9  Pruning old releases"
ssh "$SSH_HOST" "cd $DEPLOY_DIR/releases && current=\$(readlink -f $ABS_DEPLOY_DIR/current 2>/dev/null || true) && ls -1dt */ | tail -n +$((RELEASES_TO_KEEP + 1)) | while read release; do release_path=\$(readlink -f \"\$release\"); if [ \"\$release_path\" != \"\$current\" ]; then rm -rf -- \"\$release\"; fi; done"
ok "Kept newest $RELEASES_TO_KEEP releases"

# ── Restart and verify ──
step "8/9  Restarting server"
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

  step "9/9  Verifying health endpoint"
  healthy=false
  for _ in $(seq 1 15); do
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
  if $APP_ONLY; then
    warn "Skipping exe.dev proxy config (--app-only)"
  else
    ssh exe.dev share port ruckr 5000 2>/dev/null || true
    ssh exe.dev share set-public ruckr 2>/dev/null || true
  fi
fi

echo ""
echo -e "${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}  Published to exe.dev${NC}"
echo -e "${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "  ${BOLD}App:${NC}  https://ruckr.exe.xyz"
echo -e "  ${BOLD}Release:${NC}  $RELEASE_ID"
echo -e "  ${BOLD}Commit:${NC}  $GIT_COMMIT"
echo -e "  ${BOLD}Log:${NC}  ssh $SSH_HOST 'journalctl -u ruckr.service -f'"
echo -e "  ${BOLD}SQL:${NC}  ssh $SSH_HOST 'docker exec ruckr-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C'"
echo -e "${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
