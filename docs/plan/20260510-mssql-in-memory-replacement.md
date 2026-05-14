# MSSQL In-Memory Replacement for Redis

## Context
We explored adding Redis for SignalR backplane and LocationTracker storage.
Instead of that operational dependency, we can use MSSQL's built-in in-memory features.

## 1. SignalR Backplane: Use SqlServer Backplane

Replace (never-added) Redis with `Microsoft.AspNetCore.SignalR.SqlServer`.

- Package: `Microsoft.AspNetCore.SignalR.SqlServer` (v10.0.7)
- Config: `services.AddSignalR().AddSqlServer(connString, "SignalR")`
- Creates a `SignalR` schema with tables for messages/groups
- Handles multi-instance fanout via DB polling (~50ms latency)
- Tradeoff: higher latency than Redis (~50ms vs ~1ms), but zero new infra

## 2. LocationTracker: Memory-Optimized Table (In-Memory OLTP)

Replace `ConcurrentDictionary` with a memory-optimized table.

```sql
CREATE TABLE [dbo].[PlayerLocation] (
    [UserId]     NVARCHAR(450) NOT NULL PRIMARY KEY NONCLUSTERED HASH WITH (BUCKET_COUNT = 1000000),
    [Latitude]   FLOAT NOT NULL,
    [Longitude]  FLOAT NOT NULL,
    [Accuracy]   FLOAT NULL,
    [Timestamp]  DATETIME2 NOT NULL,
    [Index]      INT NOT NULL
) WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_AND_DATA);
```

- **Latency**: Memory-optimized tables give ~microsecond reads/writes (close to ConcurrentDictionary)
- **Durability**: `SCHEMA_AND_DATA` persists across restarts (bonus: no data loss on crash)
- **TTL**: Use a natively compiled stored procedure to prune old entries, or a SQL Agent job
- **Concurrency**: Row-versioning handles concurrent updates natively (no locks)
- **Bucket count**: Size to ~2x expected concurrent players (1M for 500K cap)

## 3. Rate Limiting: Already DB-Backed

`RateLimitService` already uses MSSQL. No changes needed.
Consider a memory-optimized table for rate-limit records if throughput is a concern.

## 4. Battle State: Already DB-Backed

`BattleModel` with `RowVersion` already uses EF Core + SQL Server.
The `DbUpdateConcurrencyException` pattern handles race conditions.

## 5. Cleanup: Expired Encounters & Locations

Replace eventual Redis TTL with:
- **Memory-optimized table** for `PlayerLocation` with a background `IHostedService` that runs `DELETE FROM PlayerLocation WHERE Timestamp < DATEADD(second, -60, SYSUTCDATETIME())` every 10 seconds
- Or use **temporal tables** for automatic history cleanup

## Implementation Plan

| Step | Task | Est. |
|------|------|------|
| 1 | Add `Microsoft.AspNetCore.SignalR.SqlServer` NuGet | 0.5d |
| 2 | Create memory-optimized `PlayerLocation` table via migration | 0.5d |
| 3 | Refactor `LocationTracker` to use EF Core with memory-optimized table | 1d |
| 4 | Configure SignalR SqlServer backplane in `Program.cs` | 0.5d |
| 5 | Add cleanup hosted service for expired locations | 0.5d |
| 6 | Update deploy scripts (no Redis Docker needed) | 0.5d |
| 7 | Tests | 1d |

**Total: ~4 days** (vs ~3 days for Redis, with zero new infrastructure)

## Risks

- SignalR SqlServer backplane has ~50ms latency vs Redis ~1ms. Acceptable for a turn-based game.
- Memory-optimized tables require sufficient memory allocation on the VM. For 1000 concurrent players with 60s TTL: ~1000 rows × ~100 bytes ≈ 100KB. Negligible.
- In-Memory OLTP requires SQL Server 2016+. Our Docker image (`mcr.microsoft.com/mssql/server:2022-latest`) supports it.