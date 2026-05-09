# RuckR — exe.dev Deployment

| Detail | Value |
|---|---|
| **App URL** | `https://ruckr.exe.xyz` |
| **SSH** | `ssh ruckr.exe.xyz` |
| **VM** | `ruckr` (2 CPU, 4 GB RAM, 20 GB disk) |
| **Region** | us-west-2 |
| **Image** | `boldsoftware/exeuntu` |

## Server

| Detail | Value |
|---|---|
| **Runtime** | Self-contained .NET 10 (linux-x64) |
| **Publish dir** | `~/ruckr/` |
| **WebRoot** | `~/ruckr/wwwroot/` |
| **ContentRoot** | `/home/exedev/ruckr` |
| **Listen** | `http://0.0.0.0:8000` |
| **Environment** | `Development` |
| **OTLP export** | Disabled (empty `OTEL_EXPORTER_OTLP_ENDPOINT`) |
| **Log file** | `/tmp/ruckr.log` |
| **HTTPS proxy** | exe.dev → `:8000` |

## Database

| Detail | Value |
|---|---|
| **Engine** | SQL Server 2022 (Docker) |
| **Container** | `ruckr-db` |
| **Image** | `mcr.microsoft.com/mssql/server:2022-latest` |
| **Port** | `1433` (Docker host) |
| **SA Password** | Set via `RUCKR_DB_PASSWORD` |
| **Database** | `RuckR_Dev` |
| **Connection string** | Set via `ConnectionStrings__RuckRDbContext` |
| **Migrations** | Applied (InitialCreate) |
| **Seed** | Pending — DB exists but seed hasn't run yet (connection string was wrong on first launch) |

## Configuration files

### `~/ruckr/appsettings.json`

```json
{
  "ConnectionStrings": {
    "RuckRDbContext": "Server=localhost,1433;Database=RuckR_Dev;User=sa;Password=<set-via-RUCKR_DB_PASSWORD>;TrustServerCertificate=True;"
  },
  "Seed": {
    "DefaultCenterLat": 51.5074,
    "DefaultCenterLng": -0.1278,
    "SeedValue": 42,
    "PlayerCount": 500,
    "SpreadRadiusKm": 50.0
  }
}
```

### `~/ruckr/appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "RuckRDbContext": "Server=localhost,1433;Database=RuckR_Dev;User=sa;Password=<set-via-RUCKR_DB_PASSWORD>;TrustServerCertificate=True;"
  }
}
```

## Startup command

```bash
ssh -f ruckr.exe.xyz \
  'cd ~/ruckr && nohup env \
    ASPNETCORE_URLS=http://0.0.0.0:8000 \
    ASPNETCORE_ENVIRONMENT=Development \
    OTEL_EXPORTER_OTLP_ENDPOINT= \
    ./RuckR.Server >/tmp/ruckr.log 2>&1 &'
```
