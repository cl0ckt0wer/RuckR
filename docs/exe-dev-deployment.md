# RuckR — exe.dev Deployment

| Detail | Value |
|---|---|
| App URL | `https://ruckr.exe.xyz` |
| SSH | `ssh ruckr.exe.xyz` |
| Deploy root | `/home/exedev/ruckr` |
| Source checkout | `/home/exedev/ruckr/src` |
| Releases | `/home/exedev/ruckr/releases/<release-id>` |
| Active release | `/home/exedev/ruckr/current` |
| Service | `ruckr.service` |
| Listen | `http://127.0.0.1:5000` |
| Health | `https://ruckr.exe.xyz/api/telemetry/health` |

## Canonical Command

```bash
./scripts/publish-exe-dev.sh
```

The deploy script builds on the VM from the current pushed Git commit:

1. Resolve the DB password from `RUCKR_DB_PASSWORD` or `dotnet user-secrets`.
2. Ensure SSH, .NET 10 SDK/runtime, Docker SQL Server, Jaeger, and systemd service config.
3. Write `~/ruckr/secrets.env` and `~/ruckr/app.env` with chmod `600`.
4. Back up `RuckR_Dev` and copy the `.bak` file to `C:\Users\clock\dbbackups` by default.
5. Fetch/checkout the deploy commit in `~/ruckr/src`.
6. Run `dotnet publish` on the VM into a new release directory.
7. Atomically switch `~/ruckr/current`, restart `ruckr.service`, and verify health.

Useful flags:

```bash
./scripts/publish-exe-dev.sh --yes
./scripts/publish-exe-dev.sh --ref master
./scripts/publish-exe-dev.sh --skip-restart
./scripts/publish-exe-dev.sh --app-only
```

The PowerShell entrypoint `scripts/publish-exe-dev.ps1` is a thin wrapper around the bash script so both tracked entrypoints use the same deploy logic.

## Operational Notes

- Deploys require committed and pushed code unless `--ref` points at an already fetchable commit/ref.
- The script intentionally rejects a dirty working tree for default deploys so the VM cannot build something different from local intent.
- Production secrets must not be committed. Use user-secrets locally and environment files on the VM.
- Logs: `ssh ruckr.exe.xyz 'journalctl -u ruckr.service -f'`
- SQL shell: `ssh ruckr.exe.xyz 'docker exec -it ruckr-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C'`
