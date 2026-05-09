#!/usr/bin/env bash
# Sync named Windows environment variables into this WSL session.
# Usage: source scripts/sync-windows-env.sh
# Add to ~/.bashrc for auto-sync on each new WSL terminal.

# List of Windows env vars to pull (space-separated)
VARS="RUCKR_DB_PASSWORD RUCKR_TEST_DB_PASSWORD ASPNETCORE_ENVIRONMENT ASPNETCORE_URLS"

for v in $VARS; do
  val=$(cmd.exe /c "echo %${v}%" 2>/dev/null | tr -d '\r')
  if [ "$val" != "" ] && [ "$val" != "%${v}%" ]; then
    export "$v"="$val"
  fi
done
