# RuckR Test Suite

## Overview
Playwright test suite for the RuckR rugby GPS creature-collector game. Uses Microsoft.Playwright .NET bindings with xUnit and FluentAssertions.

## Prerequisites
- .NET 10 SDK
- Docker Desktop (for SQL Server test database via Testcontainers.MsSql)
- Playwright Chromium browser (auto-installed on build)

## Quick Start

### 1. Install Playwright browsers
```bash
dotnet build RuckR.Tests
```
The first build automatically runs `playwright.ps1 install chromium` (~290 MB download). The build target is conditional — it skips in CI environments (`CI` env var set).

### 2. Ensure Docker is running
```bash
docker ps
```
Tests use `Testcontainers.MsSql` (image: `mcr.microsoft.com/mssql/server:2022-latest`) to spin up a SQL Server 2022 container automatically. The container is created and destroyed per test run.

### 3. Run tests
```bash
# Run all tests (API + E2E)
dotnet test RuckR.Tests

# Run specific test project
dotnet test RuckR.Tests\RuckR.Tests.csproj

# Run tests filtered by fully-qualified name
dotnet test RuckR.Tests --filter "FullyQualifiedName~PlayersApiTests"

# Run tests filtered by namespace
dotnet test RuckR.Tests --filter "FullyQualifiedName~RuckR.Tests.Api"
dotnet test RuckR.Tests --filter "FullyQualifiedName~RuckR.Tests.E2E"
```

## Feature Coverage Matrix

This is the current source-of-truth for website feature coverage. Keep this table updated when pages, routes, APIs, or major user flows change.

| Feature | User Surface | Coverage | Current Tests | Notes / Gaps |
|---|---|---|---|---|
| App startup and Blazor WASM loading | `/`, `_framework/*`, browser console | Covered | `StartupSmokeTests`, `BlazorWasmLoadTests` | Verifies server health, OTel registration, WASM render, framework assets, reload/cache safety, and no unexpected browser errors. |
| Authentication registration | `/Identity/Account/Register` | Covered | `AuthTests.Register_NewUser_CanLoginAndSeeNavbar`, shared `RegisterPage` use across E2E | Identity registration is exercised through real browser flows. |
| Authentication login | `/Identity/Account/Login` | Covered | `AuthTests.Root_Login_Root_Logout_CompletesSuccessfully`, `ProdLoginSmokeTest` | Local E2E uses generated test users; production smoke test requires `RUCKR_PROD_SMOKE_PASSWORD`. |
| Logout | `/Identity/Account/LogOut`, nav logout | Covered | `AuthTests.Root_Login_Root_Logout_CompletesSuccessfully`, `AuthTests.Logout_RemovesAccessToAuthenticatedPages`, `ComponentTests.NavMenuTests.NavMenu_RendersLogout_WhenAuthenticated` | New focused test verifies logout removes access to authenticated pages by redirecting `/collection` to login. |
| Navigation shell | Sidebar/nav menu | Covered | `E2E.NavMenuTests`, `ComponentTests.NavMenuTests` | Covers all visible nav links and auth-aware login/logout rendering. |
| Map and GPS | `/map` | Covered | `MapTests`, `GpsTests`, `MobileTests.MapPage_OnPixel5_ShouldBeResponsive` | Covers map render, tile load, onboarding dismissal, GPS enabled marker, GPS disabled prompt, and mobile render. |
| Catalog | `/catalog`, `PlayersController` | Covered | `CatalogTests`, `PlayersApiTests` | Covers player cards, position filter, API list/filter/detail/nearby validation. UI rarity and name-search filters are API-covered partially but not separately E2E-covered. |
| Recruitment / nearby players | `/players/nearby`, challenge buttons | Partially covered | `PvpTests.TwoUsers_ChallengeAndAccept_BothSeeResult`, `MobileTests.PlayerGrid_OnPixel5_ShouldRenderCards`, `PlayersApiTests.GetPlayersNearby_WithValidCoordinates_ReturnsSortedByDistance` | Recruitment grid renders and feeds battle challenge flow. Dedicated E2E tests for radius filter and distance labels are still desirable. |
| Collection | `/collection`, `CollectionController` | Covered | `CollectionTests`, `CollectionApiTests` | Covers unauthenticated redirect, new-user empty state, retrieve, capture, duplicate capture, missing GPS, and favorite toggle. UI favorite/capture interactions are primarily API-covered. |
| Battles / PVP | `/battle`, SignalR notifications, `BattlesController` | Covered | `PvpTests`, `BattlesApiTests`, `SignalRResiliencyTests` | Covers challenge, accept, decline, limits, pending/history API, lazy expiry, toasts, and connection health. PVP test is tolerant when no nearby users are available. |
| Battle history | `/battles/history`, `BattlesController.GetHistory` | Covered | `NavMenuTests`, `BattlesApiTests.GetHistory_ReturnsCompletedBattles` | API history behavior is covered; E2E currently verifies page navigation, not populated history rendering. |
| Pitch creation | `/pitches/create`, `PitchesController` | Covered | `PitchesApiTests`, `PitchCreateValidationTests`, `NavMenuTests` | Covers API create, nearby, duplicate, rate limit, and component validation. Full browser submit flow is still a useful future E2E test. |
| Profile | `/profile`, `ProfileController` | Partially covered | `ProfileApiTests` | API get/update behavior is covered. Profile page edit/save flow does not currently have a dedicated E2E or component test. Add one before expanding profile UI features. |
| Connection status / realtime resiliency | Shared `ConnectionStatus` component | Covered | `SignalRResiliencyTests` | Covers disconnected state, reconnect button, restored network, and healthy startup. |
| Mobile responsiveness | Pixel 5 viewport | Partially covered | `MobileTests` | Covers map and nearby player grid. Catalog, collection, battle, and pitch-create mobile layouts are not separately asserted. |
| Telemetry and health | `/api/telemetry/*`, client telemetry bridge | Covered | `TelemetryApiTests`, `StartupSmokeTests` | Covers health/status endpoints and client telemetry bridge staying healthy. |
| Deployment smoke | `https://ruckr.exe.xyz` | Manual/conditional | `ProdLoginSmokeTest` | Skips credential usage unless `RUCKR_PROD_SMOKE_PASSWORD` is set. Keep secrets out of source. |

## Test Categories

### API Tests (`RuckR.Tests/Api/`)
- Cover controllers, telemetry endpoints, and BattleHub state transitions
- Run against in-memory TestServer via `CustomWebApplicationFactory` (no browser needed)
- Fast: ~5 seconds total
- Tests by controller:
  - `PlayersApiTests` — 9 tests (list, filter by position/rarity, get by ID, 404, nearby sorted, 3 invalid-coordinate validations)
  - `PitchesApiTests` — 4 tests (create, nearby, duplicate 409, rate-limit 429)
  - `CollectionApiTests` — 5 tests (retrieve, capture 201, capture-no-GPS 400, duplicate 409, toggle favorite)
  - `BattlesApiTests` — 8 tests (challenge, self-challenge 400, 4th-pending 400, accept, decline, pending list, history, lazy-expiry)
  - `ProfileApiTests` — 2 tests (get current profile, update current profile)

### E2E Tests (`RuckR.Tests/E2E/`)
- Browser tests covering auth flow, logout, map/GPS, catalog, collection, nav menu, PVP, SignalR resiliency, mobile rendering, and startup smoke behavior
- Run against `CustomWebApplicationFactory` server + Playwright Chromium browser
- Slower: ~30 seconds per test (Blazor WASM cold starts + network idle waits)
- Tests:
  - `AuthTests` — Register → Login → See navbar → Logout → Re-login; root → login → root → logout; logout blocks authenticated pages
  - `MapTests` — Map loads with Leaflet tiles + onboarding banner dismiss
  - `CatalogTests` — Player cards render + filter by position
  - `CollectionTests` — Unauthenticated redirects to login + authenticated empty-state with CTA
  - `NavMenuTests` — All 7 nav links navigate to correct pages
  - `GpsTests` — GPS enabled user marker + disabled prompt
  - `MobileTests` — Pixel 5 map and nearby-player grid rendering
  - `PvpTests` — Two-browser challenge/accept/result notification flow
  - `SignalRResiliencyTests` — Connection state, reconnect button, and restored network
  - `StartupSmokeTests` / `BlazorWasmLoadTests` — Server health, OTel status, WASM assets, browser errors, reload/cache behavior

## Architecture

### Test Project Structure
```
RuckR.Tests/
├── RuckR.Tests.csproj          # net10.0, Playwright 1.59.0, xUnit 2.9.3, bUnit 2.7.2, Testcontainers 4.11.0
├── UnitTest1.cs                # Sanity check: TestProjectBuilds
├── Api/
│   ├── PlayersApiTests.cs      # 9 tests
│   ├── PitchesApiTests.cs      # 4 tests
│   ├── CollectionApiTests.cs   # 5 tests
│   └── BattlesApiTests.cs      # 8 tests
├── E2E/
│   ├── AuthTests.cs            # registration, login, logout, auth-page access
│   ├── MapTests.cs             # 2 tests
│   ├── CatalogTests.cs         # 2 tests
│   ├── CollectionTests.cs      # 2 tests
│   └── NavMenuTests.cs         # 1 test
├── Fixtures/
│   ├── CustomWebApplicationFactory.cs  # WebApplicationFactory<Program> with Testcontainers DB
│   ├── PlaywrightFixture.cs            # Chromium browser lifecycle + context factory
│   ├── DatabaseFixture.cs              # Standalone Testcontainers SQL Server lifecycle
│   ├── TestAuthHandler.cs              # Test authentication handler (bypasses Identity)
│   ├── TestLocationTracker.cs          # Injectable GPS position mock
│   └── TestDataFactory.cs              # Test model builders (PlayerModel, PitchModel)
├── Pages/
│   ├── BasePage.cs             # Abstract base: navigation, Blazor wait, spinner dismiss
│   ├── LoginPage.cs            # /Identity/Account/Login
│   ├── RegisterPage.cs         # /Identity/Account/Register
│   ├── NavMenu.cs              # Shared nav component (7 links + auth state)
│   ├── MapPage.cs              # /map (Leaflet tiles, onboarding, pitch markers)
│   ├── CatalogPage.cs          # /catalog (filters, player cards)
│   ├── CollectionPage.cs       # /collection (empty state, CTA)
│   ├── PlayerGridPage.cs       # /players/nearby (radius, distances, challenge)
│   ├── BattlePage.cs           # /battle (challenge form, accept modal, result)
│   ├── BattleHistoryPage.cs    # /battles/history (timeline, result badges)
│   ├── PitchCreatePage.cs      # /pitches/create (EditForm, GPS auto-fill, rate limit)
│   ├── NotificationToast.cs    # Toast notification shared component
│   └── ConnectionStatus.cs     # SignalR connection indicator
└── Helpers/
    └── BlazorWaitHelper.cs     # WaitForBlazorLoadAsync, WaitForPageFullyLoadedAsync, WaitForFluxorReadyAsync
```

### Fixtures
- **`CustomWebApplicationFactory`** (`IClassFixture`) — Inherits `WebApplicationFactory<Program>`. Overrides `ConfigureWebHost` to:
  - Replace `RuckRDbContext` connection with Docker SQL Server (Testcontainers)
  - Register `TestAuthHandler` for [Authorize] bypass via `X-Test-UserId` header
  - Replace `ILocationTracker` with `TestLocationTracker` for server-side GPS mocking
  - Exposes `CreateAuthenticatedClient(userId, username)` and `ServerBaseUrl`
  - Applies EF Core migrations and runs `SeedService.SeedIfEmptyAsync()` on init
- **`PlaywrightFixture`** (`IClassFixture` / `IAsyncLifetime`) — Manages Playwright Chromium browser lifecycle:
  - Launches headless Chromium with `--no-sandbox --disable-gpu` args
  - `NewContextAsync()` factory: creates isolated `BrowserContext` with optional geolocation permissions, GPS coordinates, mobile viewport presets, and device presets (Pixel 5, iPhone 12 via `Playwright.Devices`)
  - Disposes browser + Playwright instance on test run completion
- **`DatabaseFixture`** (`IAsyncLifetime`) — Standalone Testcontainers.MsSql container lifecycle for direct SQL Server access (separate from `CustomWebApplicationFactory`)
- **`TestDataFactory`** — Static factory methods for creating test `PlayerModel` and `PitchModel` instances with known GPS coordinates
- **`TestAuthHandler`** — `AuthenticationHandler<AuthenticationSchemeOptions>` that authenticates requests carrying `X-Test-UserId` header, bypassing ASP.NET Core Identity
- **`TestLocationTracker`** — Injectable `ILocationTracker` implementation for setting/clearing mock GPS positions per user

### Page Objects (`RuckR.Tests/Pages/`)
12 concrete Page Object classes inheriting `BasePage`:
- **`BasePage`** — Core navigation (`NavigateToAsync`), `WaitForBlazorAsync` (waits for `h1, h3, #ruckr-map, .page` or falls back to `#app` non-empty inner HTML), spinner dismissal, reconnect modal removal, screenshot capture
- **`LoginPage`** — Fill username/password, anti-CSRF token extraction, submit, wait for post-login redirect
- **`RegisterPage`** — Fill username/password/confirm, submit, wait for post-registration redirect
- **`NavMenu`** — Auth-state detection, username display, 7 individual nav links (Map, Catalog, Collection, Nearby Players, Battles, Battle History, Create Pitch), login/logout clicks
- **`MapPage`** — Map container detection, Leaflet tile load wait, onboarding banner dismiss, pitch marker counting, GPS-disabled banner detection
- **`CatalogPage`** — Player card counting, position/rarity filters, wait for catalog loaded
- **`CollectionPage`** — Empty state detection, "Explore Map" CTA visibility
- **`PlayerGridPage`** — Nearby player grid, radius selection, distance verification
- **`BattlePage`** — Challenge creation tabs, player selection, opponent entry, result display
- **`BattleHistoryPage`** — Battle timeline, result badges
- **`PitchCreatePage`** — EditForm interaction, GPS auto-fill, rate-limit handling
- **`NotificationToast`** — Toast notification appearance and text verification
- **`ConnectionStatus`** — SignalR connection indicator (`.status-dot`)

## Running E2E Tests

E2E tests run against the `CustomWebApplicationFactory`'s internal Kestrel server (`ServerBaseUrl`). The factory automatically starts a real TCP listener — no separate `dotnet run` is needed. Each test class uses both `CustomWebApplicationFactory` and `PlaywrightFixture` as `IClassFixture<>` dependencies.

```bash
# Run all E2E tests
dotnet test RuckR.Tests --filter "FullyQualifiedName~RuckR.Tests.E2E"

# Run a specific E2E test
dotnet test RuckR.Tests --filter "FullyQualifiedName~AuthTests"
```

### E2E Test Lifecycle
1. `CustomWebApplicationFactory.InitializeAsync()` starts the Docker SQL Server container, applies migrations, seeds data, and starts the Kestrel server
2. `PlaywrightFixture.InitializeAsync()` launches headless Chromium
3. Each test creates a new isolated `BrowserContext` (fresh cookies/storage)
4. Tests navigate to pages, wait for Blazor WASM to render, interact, and assert
5. Context/page are disposed per test; factory and browser persist across the test class

## Troubleshooting

### "Docker is not running"
Start Docker Desktop. The `CustomWebApplicationFactory` automatically creates and destroys SQL Server containers via `Testcontainers.MsSql`. If Docker is unavailable, test initialization will throw `DockerNotRunningException`.

### "Playwright executable not found"
```bash
dotnet build RuckR.Tests  # Auto-installs Playwright Chromium via post-build target
```
The `.csproj` contains a post-build `<Target Name="InstallPlaywrightBrowsers">` that runs `pwsh -c "& '$(OutputPath)playwright.ps1' install chromium"`. This target is skipped if the `CI` environment variable is set.

### "Test timeout waiting for Blazor render"
Blazor WASM cold starts take 5-15 seconds for .NET runtime download and JIT compilation. The `BasePage.WaitForBlazorAsync()` method has a 30-second default timeout. If your machine is slow, increase the `timeoutMs` parameter or use a lighter page (`/catalog`) as a warm-up page first.

### "Test takes too long / hangs"
- Blazor WASM has a cold-start penalty on first navigation. Tests within the same class reuse the `BrowserContext`, so the WASM runtime stays warm after the first navigation.
- NetworkIdle waits can be slow. Consider reducing `WaitForLoadStateAsync` timeouts in performance-sensitive scenarios.
- xUnit parallelization is disabled (`xunit.runner.json`: `parallelizeTestCollections=false`, `maxParallelThreads=1`) to avoid Blazor WASM memory contention and rate-limit state conflicts.

### E2E tests fail with "Connection refused"
The `CustomWebApplicationFactory` starts the server automatically. If you see connection refused:
1. Verify the factory initialized successfully (check test output for Docker container startup)
2. Check that no other process is using the auto-assigned port
3. The factory's `ServerBaseUrl` is logged on initialization — verify it's accessible

### Browser window appears during tests
By default, Chromium runs headless. To debug visually, set the environment variable:
```powershell
$env:PLAYWRIGHT_HEADED = "1"
dotnet test RuckR.Tests --filter "FullyQualifiedName~YourTest"
```
The `PlaywrightFixture` checks this env var and switches to headed mode. Combine with `--no-headless` or slow-motion debugging by modifying `BrowserTypeLaunchOptions` in the fixture.

### Rate limit (429) errors in tests
The pitch creation rate limiter tracks per-user request counts in memory. Since the `CustomWebApplicationFactory` is shared across test classes (via `IClassFixture`), rate-limit state may persist between test classes. The `PitchesApiTests.CreatePitch_RateLimitExceeded_Returns429` test creates a fresh user to ensure a clean rate-limit counter.
