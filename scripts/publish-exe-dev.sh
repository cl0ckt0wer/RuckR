#!/usr/bin/env bash
# Native bash deploy. Builds on the exe.dev VM from the current pushed Git commit,
# then atomically switches ~/ruckr/current and verifies health.
# Usage: ./scripts/publish-exe-dev.sh [--app-only] [--no-restore] [--skip-restart] [--prepare-playwright] [--yes] [--ref <git-ref>] [--host <ssh-host>] [--app-url <url>] [--deploy-dir <abs-dir>] [--service-name <name>] [--share-name <exe-dev-share>]
set -euo pipefail

SSH_HOST="ruckr.exe.xyz"
ABS_DEPLOY_DIR="/home/exedev/ruckr"
DEPLOY_DIR="$ABS_DEPLOY_DIR"
APP_URL="https://ruckr.exe.xyz"
SERVICE_NAME="ruckr.service"
SHARE_NAME="ruckr"
REMOTE_REPO_DIR="$ABS_DEPLOY_DIR/src"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SERVER_CSPROJ_REL="RuckR/Server/RuckR.Server.csproj"
SERVER_CSPROJ="$REPO_ROOT/$SERVER_CSPROJ_REL"
RELEASE_ID=$(date +%Y%m%d%H%M%S)
PREV_RELEASE=""
RELEASES_TO_KEEP="${RUCKR_RELEASES_TO_KEEP:-3}"
DB_BACKUPS_TO_KEEP="${RUCKR_DB_BACKUPS_TO_KEEP:-20}"
DEPLOY_REF="${RUCKR_DEPLOY_REF:-}"

GREEN='\033[0;32m'; CYAN='\033[0;36m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; MAGENTA='\033[0;35m'; BOLD='\033[1m'; NC='\033[0m'

APP_ONLY=false; NO_RESTORE=false; SKIP_RESTART=false; PREPARE_PLAYWRIGHT=false; NONINTERACTIVE=false
while [ "$#" -gt 0 ]; do
  case "$1" in
    --app-only) APP_ONLY=true; shift ;;
    --no-restore) NO_RESTORE=true; shift ;;
    --skip-restart) SKIP_RESTART=true; shift ;;
    --prepare-playwright) PREPARE_PLAYWRIGHT=true; shift ;;
    --yes|-y) NONINTERACTIVE=true; shift ;;
    --ref) DEPLOY_REF="${2:-}"; [ -n "$DEPLOY_REF" ] || { echo "--ref requires a value"; exit 1; }; shift 2 ;;
    --host) SSH_HOST="${2:-}"; [ -n "$SSH_HOST" ] || { echo "--host requires a value"; exit 1; }; shift 2 ;;
    --app-url) APP_URL="${2:-}"; [ -n "$APP_URL" ] || { echo "--app-url requires a value"; exit 1; }; shift 2 ;;
    --deploy-dir)
      ABS_DEPLOY_DIR="${2:-}"
      [ -n "$ABS_DEPLOY_DIR" ] || { echo "--deploy-dir requires a value"; exit 1; }
      DEPLOY_DIR="$ABS_DEPLOY_DIR"
      REMOTE_REPO_DIR="$ABS_DEPLOY_DIR/src"
      shift 2
      ;;
    --service-name) SERVICE_NAME="${2:-}"; [ -n "$SERVICE_NAME" ] || { echo "--service-name requires a value"; exit 1; }; shift 2 ;;
    --share-name) SHARE_NAME="${2:-}"; [ -n "$SHARE_NAME" ] || { echo "--share-name requires a value"; exit 1; }; shift 2 ;;
    --skip-build) echo "Unknown: --skip-build (remote-build deploy always publishes a fresh release)"; exit 1 ;;
    *) echo "Unknown: $1"; exit 1 ;;
  esac
done

step()   { echo -e "\n${CYAN}━━━ ${BOLD}$1${NC}"; }
info()   { echo -e "  ${BOLD}$1${NC} $2"; }
ok()     { echo -e "  ${GREEN}✓${NC} $1"; }
warn()   { echo -e "  ${YELLOW}⚠${NC} $1"; }
fail()   { echo -e "  ${RED}✗${NC} $1" >&2; exit 1; }

ssh() {
  command ssh -o StrictHostKeyChecking=accept-new "$@"
}

scp() {
  command scp -o StrictHostKeyChecking=accept-new "$@"
}

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

resolve_local_backup_dir(){
  if [ -n "${RUCKR_LOCAL_BACKUP_DIR:-}" ]; then
    printf '%s' "$RUCKR_LOCAL_BACKUP_DIR"
    return 0
  fi

  if [ -d "/mnt/c/Users/clock" ]; then
    printf '%s' "/mnt/c/Users/clock/dbbackups"
  elif [ -d "/c/Users/clock" ]; then
    printf '%s' "/c/Users/clock/dbbackups"
  else
    printf '%s' "$HOME/dbbackups"
  fi
}

normalize_deploy_git_remote_url(){
  local remote_url=$1

  case "$remote_url" in
    git@github.com:*.git)
      printf 'https://github.com/%s' "${remote_url#git@github.com:}"
      ;;
    git@github.com:*)
      printf 'https://github.com/%s.git' "${remote_url#git@github.com:}"
      ;;
    *)
      printf '%s' "$remote_url"
      ;;
  esac
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
step "1/10  Resolving DB password"
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
step "2/10  Pre-flight checks"
for bin in dotnet git ssh scp curl; do
  command -v "$bin" >/dev/null || fail "$bin not found on PATH"
done
ok "Local tools available (dotnet, git, ssh, scp, curl)"
ssh -o ConnectTimeout=5 -o BatchMode=yes "$SSH_HOST" "echo ok" 2>/dev/null && ok "SSH to $SSH_HOST" || fail "SSH to $SSH_HOST failed. Check connectivity and credentials."

LOCAL_BACKUP_DIR=$(resolve_local_backup_dir)
GIT_REMOTE_URL=${RUCKR_DEPLOY_GIT_REMOTE_URL:-$(git -C "$REPO_ROOT" config --get remote.origin.url)}
[ -n "$GIT_REMOTE_URL" ] || fail "No origin remote configured."
GIT_REMOTE_URL=$(normalize_deploy_git_remote_url "$GIT_REMOTE_URL")
GIT_COMMIT=$(resolve_deploy_ref)
ok "Deploy ref resolved to $GIT_COMMIT"

# ── Ensure VM infra (SDK/runtime, SQL, Jaeger) ──
step "3/10  Checking VM infrastructure"
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

  swap=$(ssh "$SSH_HOST" 'swapon --show --noheadings 2>/dev/null | wc -l')
  if [ "$swap" -eq 0 ]; then
    info "Creating" "4G swap file for remote Release publish..."
    ssh "$SSH_HOST" 'set -euo pipefail
      if [ ! -f /swapfile ]; then
        if command -v fallocate >/dev/null 2>&1; then
          sudo fallocate -l 4G /swapfile
        else
          sudo dd if=/dev/zero of=/swapfile bs=1M count=4096 status=none
        fi
        sudo chmod 600 /swapfile
        sudo mkswap /swapfile >/dev/null
      fi
      sudo swapon /swapfile
      grep -q "^/swapfile " /etc/fstab || echo "/swapfile none swap sw 0 0" | sudo tee -a /etc/fstab >/dev/null'
    ok "Swap enabled"
  else
    ok "Swap already enabled"
  fi

  sql=$(ssh "$SSH_HOST" 'docker ps --filter "name=ruckr-sql" --filter "status=running" --format "{{.Names}}" 2>/dev/null')
  if [ -z "$sql" ]; then
    info "Starting" "SQL Server container..."
    ssh "$SSH_HOST" "sudo mkdir -p /var/lib/ruckr/mssql && sudo chown -R 10001:0 /var/lib/ruckr/mssql && docker rm -f ruckr-sql 2>/dev/null; docker run -d --name ruckr-sql --restart unless-stopped -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=$(remote_quote "$PASSWORD") -v /var/lib/ruckr/mssql:/var/opt/mssql -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest"
    for _ in $(seq 1 40); do
      ready=$(ssh "$SSH_HOST" 'docker logs ruckr-sql 2>&1 | grep -q "SQL Server is now ready" && echo ready || echo waiting')
      [ "$ready" = "ready" ] && break
      sleep 3
    done
    ready=$(ssh "$SSH_HOST" 'docker logs ruckr-sql 2>&1 | grep -q "SQL Server is now ready" && echo ready || echo waiting')
    [ "$ready" = "ready" ] || fail "SQL Server did not become ready. Check docker logs ruckr-sql on $SSH_HOST."
    ok "SQL Server ready"
  else
    ok "SQL Server container running"
  fi

  jaeger=$(ssh "$SSH_HOST" 'docker ps --filter "name=ruckr-jaeger" --filter "status=running" --format "{{.Names}}" 2>/dev/null')
  jaeger_storage=$(ssh "$SSH_HOST" 'docker inspect ruckr-jaeger --format "{{range .Config.Env}}{{println .}}{{end}}" 2>/dev/null | grep "^SPAN_STORAGE_TYPE=" || true')
  jaeger_data_dir="$ABS_DEPLOY_DIR/jaeger-badger"
  if [ "$jaeger" = "ruckr-jaeger" ] && [ "$jaeger_storage" = "SPAN_STORAGE_TYPE=badger" ]; then
    ok "Jaeger container running (Badger storage)"
  else
    if [ -n "$jaeger" ]; then
      info "Recreating" "Jaeger container with Badger storage..."
    else
      info "Starting" "Jaeger container with Badger storage..."
    fi
    ssh "$SSH_HOST" "set -euo pipefail; mkdir -p $(remote_quote "$jaeger_data_dir"); docker rm -f ruckr-jaeger 2>/dev/null || true; docker run -d --name ruckr-jaeger --restart unless-stopped -p 127.0.0.1:4317:4317 -p 127.0.0.1:4318:4318 -p 127.0.0.1:16686:16686 --memory 512m -v $(remote_quote "$jaeger_data_dir"):/badger -e QUERY_BASE_PATH=/jaeger -e SPAN_STORAGE_TYPE=badger -e BADGER_EPHEMERAL=false -e BADGER_DIRECTORY_VALUE=/badger/data -e BADGER_DIRECTORY_KEY=/badger/key jaegertracing/all-in-one:latest"
    for _ in $(seq 1 20); do
      ready=$(ssh "$SSH_HOST" 'curl -fsS http://127.0.0.1:16686/jaeger/api/services >/dev/null 2>&1 && echo ready || echo waiting')
      [ "$ready" = "ready" ] && break
      sleep 2
    done
    ready=$(ssh "$SSH_HOST" 'curl -fsS http://127.0.0.1:16686/jaeger/api/services >/dev/null 2>&1 && echo ready || echo waiting')
    [ "$ready" = "ready" ] || fail "Jaeger did not become ready. Check docker logs ruckr-jaeger on $SSH_HOST."
    ok "Jaeger ready with Badger storage"
  fi
fi

# ── Deploy secrets ──
step "4/10  Deploying secrets"
ssh "$SSH_HOST" "mkdir -p $(remote_quote "$DEPLOY_DIR") && cat > $(remote_quote "$DEPLOY_DIR/secrets.env")" <<SECEOF
RUCKR_DB_PASSWORD=$PASSWORD
MSSQL_SA_PASSWORD=$PASSWORD
SECEOF
ssh "$SSH_HOST" "chmod 600 $(remote_quote "$DEPLOY_DIR/secrets.env")"

ARC_GIS_KEY=$(resolve_first_env ARC_GIS_API_KEY ArcGIS_API ArcGISApiKey)
ARC_GIS_PLACES_KEY=$(resolve_first_env ARC_GIS_PLACES_API_KEY ArcGISPlacesApiKey ArcGIS__PlacesApiKey ArcGIS__Places__ApiKey)
ARC_GIS_PORTAL_ITEM_ID=$(resolve_first_env ARC_GIS_PORTAL_ITEM_ID ArcGISPortalItemId)
GEOBLAZOR_LICENSE_KEY=$(resolve_first_env GEOBLAZOR_API GEOBLAZOR_LICENSE_KEY GEOBLAZOR_REGISTRATION_KEY GeoBlazor__LicenseKey GeoBlazor__RegistrationKey)
SEED_USER_PASSWORD=$(resolve_first_env RUCKR_SEED_USER_PASSWORD)
ARC_GIS_PLACES_KEY=${ARC_GIS_PLACES_KEY:-$ARC_GIS_KEY}
ARC_GIS_REFERRER="${APP_URL%/}/"

[ -n "$ARC_GIS_KEY" ] && ok "ArcGIS API key resolved (length: ${#ARC_GIS_KEY})" || warn "ArcGIS API key not set; map will report missing ArcGIS key"
[ -n "$ARC_GIS_PLACES_KEY" ] && ok "ArcGIS Places key resolved (length: ${#ARC_GIS_PLACES_KEY})" || warn "ArcGIS Places key not set; place candidates will be empty"
[ -n "$ARC_GIS_PORTAL_ITEM_ID" ] && ok "ArcGIS Portal Item ID resolved (length: ${#ARC_GIS_PORTAL_ITEM_ID})" || warn "ArcGIS Portal Item ID not set; map will report missing Portal Item ID"
[ -n "$GEOBLAZOR_LICENSE_KEY" ] && ok "GeoBlazor license key resolved (length: ${#GEOBLAZOR_LICENSE_KEY})" || warn "GeoBlazor license key not set; map will report missing GeoBlazor key"
[ -n "$SEED_USER_PASSWORD" ] && ok "Seed user password resolved (length: ${#SEED_USER_PASSWORD})" || warn "Seed user password not set; seed accounts will not be created or refreshed"

ssh "$SSH_HOST" "cat > $(remote_quote "$DEPLOY_DIR/app.env")" <<APPEOF
RUCKR_DB_PASSWORD=$(remote_quote "$PASSWORD")
MSSQL_SA_PASSWORD=$(remote_quote "$PASSWORD")
ConnectionStrings__RuckRDbContext=$(remote_quote "$CONNECTION_STRING")
OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4317
ArcGISApiKey=$(remote_quote "$ARC_GIS_KEY")
ARC_GIS_API_KEY=$(remote_quote "$ARC_GIS_KEY")
ArcGISPlacesApiKey=$(remote_quote "$ARC_GIS_PLACES_KEY")
ARC_GIS_PLACES_API_KEY=$(remote_quote "$ARC_GIS_PLACES_KEY")
ArcGISReferrer=$(remote_quote "$ARC_GIS_REFERRER")
ARC_GIS_REFERRER=$(remote_quote "$ARC_GIS_REFERRER")
ArcGISPortalItemId=$(remote_quote "$ARC_GIS_PORTAL_ITEM_ID")
GeoBlazor__LicenseKey=$(remote_quote "$GEOBLAZOR_LICENSE_KEY")
GeoBlazor__RegistrationKey=$(remote_quote "$GEOBLAZOR_LICENSE_KEY")
APPEOF
if [ -n "$SEED_USER_PASSWORD" ]; then
  ssh "$SSH_HOST" "cat >> $(remote_quote "$DEPLOY_DIR/app.env")" <<APPEOF
RUCKR_SEED_USER_PASSWORD=$(remote_quote "$SEED_USER_PASSWORD")
APPEOF
fi
ssh "$SSH_HOST" "chmod 600 $(remote_quote "$DEPLOY_DIR/app.env")"
ok "secrets.env and app.env deployed (chmod 600)"

# ── Ensure application database exists before backup/restart ──
step "5/10  Ensuring application database"
if $APP_ONLY; then
  warn "Skipping database creation check (--app-only)"
else
  ssh "$SSH_HOST" "DEPLOY_ENV_PATH=$(remote_quote "$ABS_DEPLOY_DIR/app.env") bash -se" <<'SSHEOF'
set -euo pipefail
set -a
. "$DEPLOY_ENV_PATH"
set +a

docker exec -i -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" ruckr-sql \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -d master <<'SQL'
IF DB_ID(N'RuckR_Dev') IS NULL
BEGIN
    CREATE DATABASE [RuckR_Dev];
END
SELECT name FROM sys.databases WHERE name = N'RuckR_Dev';
SQL
SSHEOF
  ok "RuckR_Dev database available"
fi

# ── DB backup before restart ──
step "6/10  Backing up database"
backup_output=""
if backup_output=$(ssh "$SSH_HOST" "DEPLOY_ENV_PATH=$(remote_quote "$ABS_DEPLOY_DIR/app.env") bash -se" <<'SSHEOF'
set -euo pipefail
set -a
. "$DEPLOY_ENV_PATH"
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

  if [ "$DB_BACKUPS_TO_KEEP" -gt 0 ]; then
    ssh "$SSH_HOST" "docker exec ruckr-sql bash -lc 'cd /var/opt/mssql/backup 2>/dev/null && ls -1t ruckr_*.bak 2>/dev/null | tail -n +$((DB_BACKUPS_TO_KEEP + 1)) | xargs -r rm -f'" >/dev/null 2>&1 || true
    ok "Pruned SQL backups on VM to newest $DB_BACKUPS_TO_KEEP"
  fi
else
  warn "DB backup failed (non-fatal)"
fi

# ── Install systemd service ──
step "7/10  Installing systemd service"
if $APP_ONLY; then
  warn "Skipping systemd service install (--app-only)"
else
cat <<UNIT | ssh "$SSH_HOST" "sudo tee /etc/systemd/system/$(remote_quote "$SERVICE_NAME") > /dev/null && sudo systemctl daemon-reload && sudo systemctl enable $(remote_quote "$SERVICE_NAME") > /dev/null"
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
ok "$SERVICE_NAME installed and enabled"
fi

# ── Remote checkout and publish ──
step "8/10  Building release $RELEASE_ID on VM"
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
find "\$repo" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
rm -rf "\$release_dir"
mkdir -p "\$release_dir"
/usr/share/dotnet/dotnet publish "\$repo/$SERVER_CSPROJ_REL" -c Release -o "\$release_dir" $no_restore_arg
find "\$repo" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
ln -sfn "\$release_dir" "$ABS_DEPLOY_DIR/current"
SSHEOF
ok "Built and linked current → releases/$RELEASE_ID"

step "8a/10  Playwright browser cache"
if ! $PREPARE_PLAYWRIGHT; then
  warn "Skipping Playwright browser preparation (pass --prepare-playwright when VM browser tests need it)"
else
ssh "$SSH_HOST" "RUCKR_REMOTE_REPO_DIR=$(remote_quote "$REMOTE_REPO_DIR") bash -se" <<'SSHEOF'
set -euo pipefail
repo="$RUCKR_REMOTE_REPO_DIR"
tests_project="$repo/RuckR.Tests/RuckR.Tests.csproj"

if [ ! -f "$tests_project" ]; then
  echo "Playwright setup skipped (RuckR.Tests project not found)"
  exit 0
fi

if ! command -v pwsh >/dev/null 2>&1; then
  echo "Installing PowerShell (required for playwright.ps1)..."
  wget -q https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
  sudo dpkg -i /tmp/packages-microsoft-prod.deb >/dev/null
  rm -f /tmp/packages-microsoft-prod.deb
  sudo apt-get update -qq
  sudo apt-get install -y powershell >/dev/null
fi

build_out="$repo/RuckR.Tests/bin/Release/net10.0"
/usr/share/dotnet/dotnet build "$tests_project" -c Release -p:CI=true >/dev/null
playwright_script="$build_out/playwright.ps1"

if [ ! -f "$playwright_script" ]; then
  echo "Playwright setup skipped (playwright.ps1 missing at $playwright_script)"
  exit 0
fi

pwsh -NoProfile -NonInteractive -File "$playwright_script" install --with-deps chromium >/dev/null
echo "Playwright Chromium installed on VM"
SSHEOF
  ok "Playwright runtime prepared on VM"
fi

step "8b/10  Pruning old releases"
ssh "$SSH_HOST" "cd $(remote_quote "$DEPLOY_DIR/releases") && current=\$(readlink -f $(remote_quote "$ABS_DEPLOY_DIR/current") 2>/dev/null || true) && ls -1dt */ | tail -n +$((RELEASES_TO_KEEP + 1)) | while read release; do release_path=\$(readlink -f \"\$release\"); if [ \"\$release_path\" != \"\$current\" ]; then rm -rf -- \"\$release\"; fi; done"
ok "Kept newest $RELEASES_TO_KEEP releases"

# ── Restart and verify ──
step "9/10  Restarting server"
if $SKIP_RESTART; then
  warn "Skipping restart (--skip-restart)"
else
  ssh "$SSH_HOST" "sudo systemctl restart $(remote_quote "$SERVICE_NAME")"
  sleep 2
  status=$(ssh "$SSH_HOST" "sudo systemctl is-active $(remote_quote "$SERVICE_NAME") 2>/dev/null || true")
  if [ "$status" != "active" ]; then
    warn "systemd restart failed. Rolling back to previous release..."
    if [ -n "$PREV_RELEASE" ]; then
      ssh "$SSH_HOST" "ln -sfn $(remote_quote "$PREV_RELEASE") $(remote_quote "$ABS_DEPLOY_DIR/current") && sudo systemctl restart $(remote_quote "$SERVICE_NAME")"
      warn "Rolled back to $PREV_RELEASE"
    fi
    ssh "$SSH_HOST" "sudo systemctl --no-pager status $(remote_quote "$SERVICE_NAME") | tail -20"
    fail "Restart failed"
  fi
  ok "systemd restarted (active)"

  step "10/10  Verifying health endpoint"
  healthy=false
  for _ in $(seq 1 15); do
    code=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 5 "$APP_URL/api/telemetry/health" 2>/dev/null || true)
    if [ "$code" = "200" ]; then
      healthy=true
      ok "Server healthy at $APP_URL/api/telemetry/health"
      seeded=$(ssh "$SSH_HOST" "journalctl -u $(remote_quote "$SERVICE_NAME") -n 200 --no-pager 2>/dev/null | grep -c \"Seeded\" || true")
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
    ssh "$SSH_HOST" "journalctl -u $(remote_quote "$SERVICE_NAME") -n 40 --no-pager 2>/dev/null || true"
  fi

  info "Configuring" "exe.dev proxy..."
  if $APP_ONLY; then
    warn "Skipping exe.dev proxy config (--app-only)"
  else
    ssh exe.dev share port "$SHARE_NAME" 5000 2>/dev/null || true
    ssh exe.dev share set-public "$SHARE_NAME" 2>/dev/null || true
  fi
fi

echo ""
echo -e "${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}  Published to exe.dev${NC}"
echo -e "${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "  ${BOLD}App:${NC}  $APP_URL"
echo -e "  ${BOLD}Release:${NC}  $RELEASE_ID"
echo -e "  ${BOLD}Commit:${NC}  $GIT_COMMIT"
echo -e "  ${BOLD}Log:${NC}  ssh $SSH_HOST 'journalctl -u $SERVICE_NAME -f'"
echo -e "  ${BOLD}SQL:${NC}  ssh $SSH_HOST 'docker exec ruckr-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C'"
echo -e "${MAGENTA}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
