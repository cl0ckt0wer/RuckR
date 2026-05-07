# AGENTS.md

## Active Development

| Goal | Status | Detail |
| ---- | ------ | ------ |
| Upgrade to .NET 10 | complete | All three projects now target `net10.0`; NuGet packages updated to 10.0.7. |
| Finish Profile feature | complete | `ProfileModel` expanded (6 properties + DataAnnotations), `ProfileController` (GET/PUT), and `Profile.razor` (view/edit toggle with EditForm validation) fully implemented. |
| Update NuGet packages | complete | All NuGet references updated to 10.0.7 via `dotnet` CLI. |
| Creature Collector Rugby GPS | planned | Transform RuckR into a rugby-themed creature-collector game with GPS integration, real-time map, PVP battles, and persistent storage. See `docs/plan/20260505-creature-collector-rugby-gps/PRD.yaml`. |

### Architectural Decisions

- **ADR-001:** Target framework upgrade to **.NET 10** (from current .NET 8). Affects all `.csproj` files and NuGet package versions.
- **ADR-002:** Profile feature follows the **existing controller pattern** (`WeatherForecastController`): `[ApiController]` + `[Route("[controller]")]` in `RuckR.Server.Controllers`, model in `RuckR.Shared.Models`, Razor page in `RuckR.Client.Pages`.
- **ADR-003:** NuGet packages updated via `dotnet` CLI (`dotnet list package --outdated`, `dotnet add package`); no external tooling.
- **ADR-004:** Spatial data layer — **SQL Server geography type + NetTopologySuite** for geospatial queries (proximity searches, pitch discovery). Rationale: User chose MSSQL for Windows dev simplicity (LocalDB); EF Core supports it via `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite`.
- **ADR-005:** Real-time communication — **SignalR** via `Microsoft.AspNetCore.SignalR` for live map updates, battle notifications, and player location sharing. Rationale: SignalR is the idiomatic ASP.NET Core real-time library with built-in Blazor client support.
- **ADR-006:** Map rendering — **Leaflet.js via JS interop** (`IJSObjectReference`/`IJSRuntime`) for the interactive map. Rationale: Leaflet is lightweight (42 KB), open-source, and has mature Blazor interop community patterns; avoids heavy SDK dependencies in WASM.
- **ADR-007:** Player data model redesign — new models under `RuckR.Shared.Models` namespace: `PlayerModel` (creature), `PitchModel` (location), `CollectionModel` (user inventory), `BattleModel` (async PVP state). Follows existing `ProfileModel` convention (subdirectory with qualified namespace).
- **ADR-008:** State management — **Fluxor** (Flux/Redux pattern for Blazor) for client-side state (inventory, active battles, map state). Rationale: Multiple pages need shared reactive state; Fluxor is the most popular Blazor state library with middleware, dev tools, and strong community adoption.
- **ADR-009:** Authentication — **ASP.NET Core Identity** with **EF Core** for user registration/login. Razor pages for auth UI (login, register) served by the Server project. Rationale: ASP.NET Core Identity is the built-in, battle-tested auth framework; avoids third-party auth dependencies for a simple username/password MVP.

## Build & Run

```sh
dotnet build RuckR.sln
```

Run the server (it hosts both the API and the Blazor WASM client):

```sh
dotnet run --project RuckR\Server\RuckR.Server.csproj --launch-profile https
```

- `https` profile serves on `https://localhost:7161` and `http://localhost:5282`.
- The Client project is **never run standalone** — the Server project serves the compiled WASM assets.

**Server is a long-lived process.** `dotnet run` starts the ASP.NET Core server which runs until explicitly stopped. Do NOT run it synchronously in a bash call or the session will hang forever. Always run it in the background:

```powershell
Start-Job -Name RuckRServer { dotnet run --project RuckR\Server\RuckR.Server.csproj --launch-profile https }
```

To check if it's running:

```powershell
Receive-Job -Name RuckRServer
```

To stop it:

```powershell
Stop-Job -Name RuckRServer
Remove-Job -Name RuckRServer
```

## Architecture

| Project          | Role                                                  |
| ---------------- | ----------------------------------------------------- |
| `RuckR.Server`   | ASP.NET Core host; API controllers; serves WASM files |
| `RuckR.Client`   | Blazor WebAssembly SPA (runs in browser)              |
| `RuckR.Shared`   | Models/types shared by both Server and Client         |

- API endpoints → `RuckR/Server/Controllers/` (controllers)
- Razor pages → `RuckR/Client/Pages/` (`.razor` files)
- Shared models → `RuckR/Shared/` or `RuckR/Shared/Models/`
- Client layout/nav → `RuckR/Client/Shared/`

## Conventions

- Target framework: `net10.0` across all three projects. NuGet packages are version 10.0.7.
- Nullable reference types are enabled in all three projects.
- The `SupportedPlatform include="browser"` on `RuckR.Shared` marks that assembly for WASM compatibility.
- Service worker (`service-worker.js` / `service-worker.published.js`) is registered for PWA support on the client.
- No test projects, linting config, CI/CD, or editorconfig exist yet.

## agentmemory

Persistent memory across sessions — captures decisions, bugs, patterns, and architecture knowledge. Docker stack + Ollama embeddings + OpenRouter LLM. 51 MCP tools available.

> **Full documentation:** Load the skill via `/agentmemory-setup` or read `~/.config/opencode/skills/agentmemory-setup/SKILL.md`.

**Quick health check:**
```powershell
docker ps --filter "name=agentmemory"   # both containers should be Up
curl http://localhost:3111/agentmemory/health  # should return "healthy"
```

**Start if not running:**
```powershell
docker compose -f $env:USERPROFILE\.agentmemory-docker\docker-compose.yml up -d
Start-Process -FilePath "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe" -ArgumentList "serve" -WindowStyle Hidden
```

### Proposed Convention Changes (flagged — not auto-accepted)

> These proposals surfaced during the .NET 10 upgrade. Review and accept/reject as needed.

| # | Proposal | Rationale | Status |
| -- | -------- | --------- | ------ |
| PCC-01 | **Update TFM convention from `net8.0` to `net10.0`** | .NET 10 upgrade is complete; all three `.csproj` files target `net10.0`; NuGet packages are 10.0.7. Convention and Active Development table updated accordingly. | `✅ accepted` |
| PCC-02 | **Document block-scoped namespace convention** | `WeatherForecastController.cs` and the new `ProfileController.cs` use block-scoped namespaces (`namespace X { ... }`), not file-scoped (`namespace X;`). This is the established project style and should be documented to ensure consistency. | `🟡 flagged` |
| PCC-03 | **Document Router NotFoundPage migration pattern** | .NET 10 Router requires `NotFoundPage` parameter + separate `NotFound.razor` page (removes `<NotFound>` fragment). `_Imports.razor` needs `@using RuckR.Client.Pages` for type resolution. This is a permanent pattern for all future Router usage. | `🟡 flagged` |
| PCC-04 | **Clarify ProfileModel namespace convention** | `ProfileModel` lives in `RuckR.Shared.Models` (subdirectory). `WeatherForecast` is in `RuckR.Shared` (root directory). Both conventions coexist — models in subdirectories use qualified namespaces. Document this to avoid future relocation attempts. | `🟡 flagged` |
| PCC-05 | **Document SDK-managed packages exclusion** | `dotnet list package --outdated` shows SDK-managed packages (`ILLink.Tasks`, `WebAssembly.Pack`) with auto-referenced versions. These should **not** be manually upgraded — the SDK manages them. Flag to prevent confusion during package audits. | `🟡 flagged` |
