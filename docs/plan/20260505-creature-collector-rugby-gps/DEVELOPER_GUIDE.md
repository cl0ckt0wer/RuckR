# RuckR Developer Guide

> Complete architecture, setup, and API reference for the RuckR Creature Collector — a rugby-themed Blazor WASM GPS game.

## Table of Contents

1. [Getting Started](#1-getting-started)
2. [Architecture Overview](#2-architecture-overview)
3. [API Reference](#3-api-reference)
4. [Spatial Data Guide](#4-spatial-data-guide)
5. [Fluxor State Store Reference](#5-fluxor-state-store-reference)
6. [SignalR Event Flow](#6-signalr-event-flow)
7. [FAQ / Gotchas](#7-faq--gotchas)

---

## 1. Getting Started

### Prerequisites

- **.NET 10 SDK** — download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0)
- **SQL Server** — one of:
  - **LocalDB** (Windows only, zero-config) — included with Visual Studio or the .NET SDK workload
  - **Docker SQL Server** (cross-platform) — `docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Pass" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest`
- **Git** — to clone the repository

### Clone and Restore

```sh
git clone <repository-url> RuckR
cd RuckR
dotnet restore RuckR.sln
```

### Database Setup

Apply the EF Core migration to create all tables (Identity tables + game tables with spatial indexes):

```sh
dotnet ef database update --project RuckR/Server
```

This creates the `RuckR` database in your configured SQL Server instance with:
- ASP.NET Core Identity tables (`AspNetUsers`, `AspNetRoles`, etc.)
- Game tables: `Players`, `Pitches`, `Collections`, `Battles`
- Spatial indexes on `Pitches(Location)` and `Players(SpawnLocation)` using `GEOGRAPHY_AUTO_GRID`

If you prefer Docker SQL Server, update the connection string in `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "RuckRDbContext": "Server=localhost,1433;Database=RuckR_Dev;User Id=sa;Password=YourStrong!Pass;TrustServerCertificate=True;"
  }
}
```

### Seed Data

On first run, the **`SeedService`** checks whether the `Players` table is empty. If empty, it auto-generates **500 fictional rugby players** centered around a configurable location (default: **London, 51.5074°N, 0.1278°W**). A default pitch named *"RuckR Training Ground"* is also created.

Configure the seed behavior in `appsettings.json`:

```json
{
  "Seed": {
    "DefaultCenterLat": 51.5074,
    "DefaultCenterLng": -0.1278,
    "SeedValue": 42,
    "PlayerCount": 500,
    "SpreadRadiusKm": 50.0
  }
}
```

Changing `SeedValue` produces a different set of players. The seed center can be changed to match your desired metropolitan area.

### Run

```sh
dotnet run --project RuckR/Server/RuckR.Server.csproj --launch-profile https
```

The Server project hosts both the API and the compiled Blazor WASM client. The Client project is **never run standalone**.

| Profile | URL |
|---------|-----|
| HTTPS | `https://localhost:7161` |
| HTTP | `http://localhost:5282` |

### Verify

1. Open `https://localhost:7161` — you should see the Map page (default route)
2. Register an account at `/Identity/Account/Register`
3. Log in at `/Identity/Account/Login`
4. Grant GPS permissions when prompted by the browser

---

## 2. Architecture Overview

### Solution Structure

| Project | Target | Role |
|---------|--------|------|
| `RuckR.Server` | `net10.0` | ASP.NET Core host: API controllers, SignalR BattleHub, Identity auth, EF Core DbContext, serves WASM files |
| `RuckR.Client` | `net10.0` | Blazor WebAssembly SPA: Razor pages, Fluxor store, SignalR client, Leaflet JS interop, browser Geolocation API |
| `RuckR.Shared` | `net10.0` (browser) | Models, enums, DTOs shared by both Server and Client; `SupportedPlatform include="browser"` for WASM compatibility |

### Data Flow

```
┌──────────────────────────────────────────────────────────────────────────┐
│                            BROWSER (WASM)                                │
│                                                                          │
│  ┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐   │
│  │  Razor Pages      │    │  Fluxor Store    │    │  JS Modules      │   │
│  │  (Map, Catalog,   │◄──►│  (Location, Map, │    │  (Leaflet map,   │   │
│  │   Collection,     │    │   Game, Inventory,│    │   Geolocation)   │   │
│  │   Battle, etc.)   │    │   Battle)        │    │                  │   │
│  └────────┬──────────┘    └────────┬─────────┘    └────────┬─────────┘   │
│           │                        │                        │            │
│           │    ┌───────────────────▼──────────────────┐     │            │
│           │    │  ApiClientService                    │     │            │
│           │    │  SignalRClientService                │     │            │
│           │    │  GeolocationService                  │     │            │
│           │    │  MapService                          │     │            │
│           │    └──────┬──────────────┬────────────────┘     │            │
│           │           │   HTTP/REST  │  WebSocket/SignalR   │            │
└───────────┼───────────┼──────────────┼──────────────────────┼────────────┘
            │           │              │                      │
┌───────────┼───────────┼──────────────┼──────────────────────┼────────────┐
│           │           │   SERVER     │                      │            │
│           │           ▼              ▼                      │            │
│  ┌────────┴──────┐  ┌─────────────────────────┐            │            │
│  │  Controllers   │  │  BattleHub (SignalR)    │            │            │
│  │  (Players,     │  │  - SendChallenge        │            │            │
│  │   Pitches,     │  │  - AcceptChallenge      │            │            │
│  │   Collection,  │  │  - DeclineChallenge     │            │            │
│  │   Battles,     │  │  - UpdateLocation       │            │            │
│  │   Profile)     │  │  - JoinBattleGroup      │            │            │
│  └───────┬────────┘  └───────────┬─────────────┘            │            │
│          │                       │                          │            │
│          │    ┌──────────────────▼────────────┐             │            │
│          │    │  Services                     │             │            │
│          │    │  - BattleResolver             │             │            │
│          │    │  - LocationTracker (singleton)│             │            │
│          │    │  - PlayerGeneratorService     │             │            │
│          │    │  - SeedService                │             │            │
│          │    └──────────────┬───────────────┘             │            │
│          │                   │                             │            │
│          │    ┌──────────────▼───────────────┐             │            │
│          │    │  RuckRDbContext (EF Core)     │             │            │
│          │    │  DbSets: Players, Pitches,    │             │            │
│          │    │  Collections, Battles         │             │            │
│          │    │  + Identity Tables            │             │            │
│          │    └──────────────┬───────────────┘             │            │
│          │                   │                             │            │
│          │    ┌──────────────▼───────────────┐             │            │
│          │    │  SQL Server                   │             │            │
│          │    │  (geography type, SRID 4326)  │             │            │
│          │    └──────────────────────────────┘             │            │
└──────────────────────────────────────────────────────────────────────────┘
```

### Real-Time Flow

```
Client A                        Server (BattleHub)                     Client B
   │                                  │                                    │
   │── UpdateLocation(lat,lng) ──────►│                                    │
   │                                  │── stores in ILocationTracker       │
   │                                  │── checks pitch proximity           │
   │◄── PitchDiscovered(pitch) ──────│                                    │
   │                                  │                                    │
   │── SendChallenge(userB,playerId)►│                                    │
   │                                  │── validates (not self, ≤3 pending) │
   │                                  │── persists BattleModel              │
   │                                  │──── ReceiveChallenge(notif) ──────►│
   │                                  │                                    │
   │                                  │◄─── AcceptChallenge(id,playerId) ─│
   │                                  │── BattleResolver.Resolve()         │
   │                                  │── updates BattleModel (Completed)  │
   │◄──── BattleResolved(result) ────│──── BattleResolved(result) ───────►│
```

### Map Rendering Flow

```
Blazor WASM                    JavaScript (leaflet-map.module.js)       External
   │                                   │                                   │
   │── IJSObjectReference              │                                   │
   │   .InvokeAsync("initMap") ───────►│                                   │
   │                                   │── L.map(containerId, options)     │
   │                                   │── L.tileLayer(OSM URL)            │
   │                                   │── Load tiles ────────────────────►│
   │                                   │                                   │ OpenStreetMap
   │                                   │◄── map tiles ───────────────────│
   │◄── map initialized ──────────────│                                   │
   │                                   │                                   │
   │── AddPitchMarkersAsync(markers)──►│                                   │
   │                                   │── L.divIcon (🏉 emoji) per marker │
   │                                   │── marker.on('click', callback)   │
   │                                   │── DotNet.invokeMethodAsync(...)  │
   │◄── pitch clicked (callback) ─────│                                   │
   │                                   │                                   │
   │── CenterOnAsync(lat, lng) ───────►│                                   │
   │                                   │── map.panTo([lat, lng])          │
```

---

## 3. API Reference

All controllers follow the block-scoped namespace pattern, `[ApiController]` + `[Route("[controller]")]`. Auth is cookie-based ASP.NET Core Identity (same-origin). `[Authorize]` is annotated per-endpoint below.

### 3.1 Players (`/players`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/players` | Required | Catalog with optional filters: `?position=Prop&rarity=Epic&name=Ruck` |
| `GET` | `/players/{id}` | Required | Single player detail |
| `GET` | `/players/nearby?lat=&lng=&radius=` | Required | Proximity search for players near a coordinate |

**GET /players**

```
GET /players?position=Prop&rarity=Epic
```

Filters:
- `position` (optional) — `Prop`, `Hooker`, `Lock`, `Flanker`, `ScrumHalf`, `FlyHalf`, `Centre`, `Wing`, `Fullback`
- `rarity` (optional) — `Common`, `Uncommon`, `Rare`, `Epic`, `Legendary`
- `name` (optional) — case-sensitive substring search

Response `200`:
```json
[
  {
    "id": 1,
    "name": "Ruck Mauler",
    "position": "Prop",
    "team": "Thunderstrike RFC",
    "speed": 45,
    "strength": 92,
    "agility": 38,
    "kicking": 22,
    "rarity": "Epic",
    "spawnLocation": { "X": -0.1278, "Y": 51.5074, "SRID": 4326 },
    "bio": "A powerhouse of the front row..."
  }
]
```

**GET /players/nearby**

```
GET /players/nearby?lat=51.5074&lng=-0.1278&radius=10000
```

| Parameter | Type | Range | Default |
|-----------|------|-------|---------|
| `lat` | double | -90..90 | (required) |
| `lng` | double | -180..180 | (required) |
| `radius` | double | 1..50000 | 10000 |

The `radius` is capped at **50,000m** server-side. Results are sorted by exact distance (server-side). The response uses `NearbyPlayerDto`:

Response `200`:
```json
[
  {
    "playerId": 42,
    "name": "Flanker Flash",
    "position": "Flanker",
    "rarity": "Rare",
    "fuzzyDistanceMeters": 1234.56,
    "ownerUsername": "rugby_fan_99"
  }
]
```

> **Note:** The server returns exact distance in `fuzzyDistanceMeters`. The client applies a ±20% random fuzz factor per session before display. User GPS coordinates are never exposed to other players.

### 3.2 Pitches (`/pitches`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/pitches?page=&pageSize=` | No | Paginated pitch list |
| `GET` | `/pitches/nearby?lat=&lng=&radius=` | No | Proximity search |
| `GET` | `/pitches/{id}` | No | Single pitch detail |
| `POST` | `/pitches` | Required | Create pitch (rate limited 5/day) |

**POST /pitches**

```
POST /pitches
Content-Type: application/json

{
  "name": "Scrum Hollow",
  "latitude": 51.5080,
  "longitude": -0.1280,
  "type": "Standard"
}
```

Request body: `CreatePitchRequest`
| Field | Type | Constraints |
|-------|------|-------------|
| `name` | string | Required, max 200 chars |
| `latitude` | double | -90..90 |
| `longitude` | double | -180..180 |
| `type` | string | `Standard`, `Training`, or `Stadium` |

Validation:
- Name must be unique within **100m radius** of the requested location → `409 Conflict` if duplicate
- Max **5 pitches per user per 24-hour period** → `429 Too Many Requests`

Response `201 Created`:
```json
{
  "id": 3,
  "name": "Scrum Hollow",
  "location": { "X": -0.1280, "Y": 51.5080, "SRID": 4326 },
  "creatorUserId": "abc123...",
  "type": "Standard",
  "createdAt": "2026-05-06T12:00:00Z"
}
```

### 3.3 Collection (`/collection`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/collection` | Required | User's collected players (with Player navigation property) |
| `POST` | `/collection/capture` | Required | Capture a player at a pitch (GPS validated) |
| `POST` | `/collection/{id}/favorite` | Required | Toggle favorite flag |

**POST /collection/capture**

```
POST /collection/capture
Content-Type: application/json

{
  "playerId": 42,
  "pitchId": 3
}
```

Capture validation (server-side):
1. **GPS required** — user must have sent a position via SignalR `UpdateLocation` within the last **60 seconds** with accuracy < 50m → `400 Bad Request "GPS position required"` if stale/missing
2. **Proximity check** — user must be within **100m** of the pitch (`IsWithinDistance`) → `400` if not
3. **Ownership check** — user must not already own this player (unique constraint on `(UserId, PlayerId)`) → `409 Conflict` if duplicate
4. **Rate limit** — max **20 captures per hour** per user → `429`

Response `201 Created`:
```json
{
  "id": 7,
  "userId": "abc123...",
  "playerId": 42,
  "player": { /* PlayerModel */ },
  "capturedAt": "2026-05-06T12:01:00Z",
  "isFavorite": false,
  "capturedAtPitchId": 3
}
```

### 3.4 Battles (`/battles`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `POST` | `/battles/challenge` | Required | Send a challenge |
| `POST` | `/battles/{id}/accept` | Required | Accept and resolve a challenge |
| `POST` | `/battles/{id}/decline` | Required | Decline a challenge |
| `GET` | `/battles/pending` | Required | Pending challenges (incoming + outgoing) |
| `GET` | `/battles/history` | Required | Battle history (completed/declined/expired) |

**POST /battles/challenge**

```
POST /battles/challenge
Content-Type: application/json

{
  "opponentUsername": "rugby_fan_99",
  "selectedPlayerId": 42
}
```

Validation:
- Cannot challenge yourself → `400`
- Opponent must exist → `404`
- Player must be in your collection → `400`
- Max **3 pending** challenges at a time → `400`
- Rate limit: **10 challenges per hour** → `429`

Response `201 Created`:
```json
{
  "id": 15,
  "challengerId": "abc123...",
  "opponentId": "def456...",
  "challengerPlayerId": 42,
  "opponentPlayerId": 0,
  "status": "Pending",
  "winnerId": null,
  "createdAt": "2026-05-06T12:05:00Z",
  "resolvedAt": null,
  "rowVersion": "AAAAAAAAB9E="
}
```

**POST /battles/{id}/accept**

```
POST /battles/15/accept
Content-Type: application/json

{
  "selectedPlayerId": 88
}
```

Validation:
- Only the opponent can accept → `403 Forbid`
- Battle must be in `Pending` status → `400`
- Challenges older than **24 hours** are auto-expired → `410 Gone`
- Selected player must be in your collection → `400`

The accept endpoint transitions the battle to `Accepted` status. The actual battle resolution (stat comparison) happens via the **BattleHub** SignalR hub, not this REST endpoint. The REST endpoint handles the initial state transition only.

Response `200`:
```json
{
  "id": 15,
  "status": "Accepted",
  "opponentPlayerId": 88,
  "resolvedAt": "2026-05-06T12:06:00Z"
}
```

**GET /battles/pending**

Returns both incoming and outgoing pending challenges. **Lazy expiry**: challenges older than 24 hours are auto-expired and persisted before returning results.

Response `200`:
```json
[
  {
    "id": 15,
    "challengerId": "abc123...",
    "opponentId": "def456...",
    "status": "Pending",
    "createdAt": "2026-05-06T12:05:00Z"
  }
]
```

### 3.5 Profile (`/profile`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/profile` | Required | Get current profile |
| `PUT` | `/profile` | Required | Update profile |

> Note: The Profile endpoint uses in-memory static storage (no database persistence). For the MVP, it serves as a user settings page.

---

## 4. Spatial Data Guide

### NetTopologySuite (NTS) Coordinate Convention

**Critical:** NTS uses `X = longitude, Y = latitude` — the **reverse** of human-readable `(lat, lng)` order.

```csharp
// CORRECT: new Point(longitude, latitude) { SRID = 4326 }
var london = new Point(-0.1278, 51.5074) { SRID = 4326 };

// Also correct (using factory):
var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
var point = factory.CreatePoint(new Coordinate(longitude, latitude));
```

| System | X axis | Y axis |
|--------|--------|--------|
| **NetTopologySuite** (server C#) | Longitude | Latitude |
| **SQL Server geography** (STDistance) | Longitude | Latitude |
| **Leaflet.js** (client JS) | Latitude | Longitude `[lat, lng]` |

> When passing coordinates between Blazor C# and Leaflet JS, you must swap the order.

### SQL Server Geography Type

All spatial columns use the **`geography`** type (not `geometry`):

- **SRID 4326** = WGS 84 (GPS coordinate system)
- **Distances returned in meters** (ellipsoidal model, ≤0.25% deviation from true geodesic distance)
- **`STDistance()`** = server-side distance computation used by EF Core

```sql
-- Underlying SQL generated by EF Core:
SELECT * FROM Pitches
WHERE Location.STDistance(@point) <= @radius  -- IsWithinDistance
ORDER BY Location.STDistance(@point) ASC      -- OrderBy(Distance)
```

### Key EF Core LINQ Patterns

**Proximity query (FindWithinRadius)**

```csharp
var userPoint = new Point(lng, lat) { SRID = 4326 };

var nearbyPitches = await _db.Pitches
    .Where(p => p.Location.IsWithinDistance(userPoint, 500))
    .ToListAsync();
// SQL: WHERE Location.STDistance(@userPoint) <= 500
```

**Nearest-neighbor query**

```csharp
var nearest = await _db.Pitchers
    .OrderBy(p => p.Location.Distance(userPoint))
    .Take(10)
    .ToListAsync();
// SQL: SELECT TOP(10) ... ORDER BY Location.STDistance(@userPoint)
```

**Distance in projection**

```csharp
var withDistance = await _db.Players
    .Where(p => p.SpawnLocation != null)
    .Select(p => new {
        p.Name,
        Distance = p.SpawnLocation!.Distance(userPoint)
    })
    .OrderBy(x => x.Distance)
    .ToListAsync();
```

### Spatial Indexes

Defined via raw SQL in the EF Core migration (`InitialCreate`):

```sql
CREATE SPATIAL INDEX [IX_Pitches_Location]
ON [Pitches] ([Location])
USING GEOGRAPHY_AUTO_GRID
WITH (CELLS_PER_OBJECT = 16);

CREATE SPATIAL INDEX [IX_Players_SpawnLocation]
ON [Players] ([SpawnLocation])
USING GEOGRAPHY_AUTO_GRID
WITH (CELLS_PER_OBJECT = 16);
```

Spatial indexes require the table to have a primary key. They significantly accelerate `STDistance()` and `IsWithinDistance()` queries.

### Client-Side Distance (Haversine)

The `GeoPosition` record in `RuckR.Shared.Models` provides a static `HaversineDistance` method for client-side calculations when a server round-trip is not needed:

```csharp
public static double HaversineDistance(GeoPosition a, GeoPosition b)
```

> **Do not** use NTS `Point.Distance()` on the client — it ignores SRID and returns **degrees**, not meters.

### Fuzzy Distance

The PRD requires that exact GPS coordinates are never exposed to other players. The fuzzy distance pattern:

1. **Server** returns exact distance in meters (via SQL Server `STDistance()`)
2. **Client** applies a ±20% random factor, cached per player per session
3. **Display** buckets the fuzzed distance: `< 50m`, `< 100m`, `< 250m`, `< 500m`, or `~{N}km`

```csharp
// Client-side fuzzy code (see PlayerGrid.razor / NearbyPlayerDto)
private static readonly Dictionary<int, double> _fuzzCache = new();
private double ApplyFuzz(int playerId, double exactMeters)
{
    if (!_fuzzCache.TryGetValue(playerId, out double fuzz))
    {
        fuzz = 0.8 + Random.Shared.NextDouble() * 0.4; // 0.8-1.2
        _fuzzCache[playerId] = fuzz;
    }
    return exactMeters * fuzz;
}
```

---

## 5. Fluxor State Store Reference

Fluxor implements the **Flux/Redux pattern**: Actions → Reducers → State → UI subscription. The store is initialized in `RuckR.Client/Program.cs`:

```csharp
builder.Services.AddFluxor(o => o.ScanAssemblies(typeof(Program).Assembly).UseReduxDevTools());
```

A `StoreInitializer` component is rendered in `App.razor` before the Router.

### Feature Breakdown

| Feature | State Record | Key Properties | Purpose |
|---------|-------------|----------------|---------|
| **LocationFeature** | `LocationState` | `UserLatitude?`, `UserLongitude?`, `AccuracyMeters?`, `IsWatching`, `ErrorMessage?` | User's GPS state from browser Geolocation API |
| **MapFeature** | `MapState` | `IsMapInitialized`, `IsMapReady`, `VisiblePitches` (IReadOnlyList), `SelectedPitchId?` | Map init state, visible pitches, selected pitch |
| **GameFeature** | `GameState` | `IsAuthenticated`, `Username?`, `IsSignalRConnected`, `ConnectionError?` | Auth status, SignalR connection state |
| **InventoryFeature** | `InventoryState` | `IsLoading`, `CollectedPlayers` (IReadOnlyList\<CollectionModel\>), `LastSynced?`, `ErrorMessage?` | User's collected players from CollectionModel |
| **BattleFeature** | `BattleState` | `IsLoading`, `ActiveChallenges` (IReadOnlyList\<BattleModel\>), `BattleHistory` (IReadOnlyList\<BattleModel\>), `ErrorMessage?` | Active challenges + battle history |

### Actions → Reducers → State Pattern

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  Service     │────►│  IDispatcher │────►│  Reducer     │────►│  State       │
│  dispatches  │     │  .Dispatch() │     │  (pure        │     │  (immutable  │
│  action      │     │              │     │   static       │     │   record)    │
│              │     │              │     │   method)      │     │              │
└──────────────┘     └──────────────┘     └──────────────┘     └──────┬───────┘
                                                                      │
                                                              ┌───────▼───────┐
                                                              │  Component    │
                                                              │  subscribes   │
                                                              │  via IState<T>│
                                                              └───────────────┘
```

### State Definition Example

```csharp
// RuckR.Client/Store/LocationFeature/LocationState.cs
[FeatureState]
public record LocationState
{
    public double? UserLatitude { get; init; }
    public double? UserLongitude { get; init; }
    public double? AccuracyMeters { get; init; }
    public bool IsWatching { get; init; }
    public string? ErrorMessage { get; init; }

    public LocationState() { } // Required by Fluxor
}
```

### Subscribing in a Razor Component

```razor
@inject IState<LocationState> LocationState

@code {
    protected override void OnInitialized()
    {
        LocationState.StateChanged += (s, e) => StateHasChanged();
    }

    // Access: LocationState.Value.UserLatitude
}
```

### SignalR → Fluxor Bridge

The `SignalRClientService` accepts `IDispatcher` via DI and dispatches Fluxor actions directly in SignalR event handlers:

```csharp
public SignalRClientService(IDispatcher dispatcher, NavigationManager navigation)
{
    _dispatcher = dispatcher;
}

// In a SignalR callback:
private void HandleReceiveChallenge(ChallengeNotification notification)
{
    var battle = new BattleModel { ... };
    _dispatcher.Dispatch(new ChallengeReceivedAction(battle));
}
```

---

## 6. SignalR Event Flow

### Hub Registration

**Server (`Program.cs`):**
```csharp
builder.Services.AddSignalR();
// ...
app.MapHub<BattleHub>("/battlehub");
```

**Client (`SignalRClientService.cs`):**
```csharp
_hubConnection = new HubConnectionBuilder()
    .WithUrl(Navigation.ToAbsoluteUri("/battlehub"))
    .WithAutomaticReconnect(new[]
    {
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    })
    .Build();
```

### Hub Methods (Client → Server)

| Client calls | Server method | Parameters | Description |
|-------------|---------------|------------|-------------|
| `SendAsync("UpdateLocation")` | `UpdateLocation` | `(double lat, double lng)` | Send GPS position; triggers pitch proximity check |
| `SendAsync("SendChallenge")` | `SendChallenge` | `(string opponentUsername, int playerId)` | Challenge another user; notifies opponent |
| `SendAsync("AcceptChallenge")` | `AcceptChallenge` | `(int battleId, int playerId)` | Accept and resolve battle; notifies both users |
| `SendAsync("DeclineChallenge")` | `DeclineChallenge` | `(int battleId)` | Decline challenge; notifies challenger |
| `SendAsync("JoinBattleGroup")` | `JoinBattleGroup` | `(int battleId)` | Join SignalR group for battle updates |
| `SendAsync("LeaveBattleGroup")` | `LeaveBattleGroup` | `(int battleId)` | Leave battle group |

### Hub Events (Server → Client)

| Server sends | Client handler | Payload type | Description |
|-------------|----------------|-------------|-------------|
| `SendAsync("PitchDiscovered")` | `On<PitchModel>("PitchDiscovered")` | `PitchModel` | A pitch is within 100m of user's position |
| `SendAsync("ReceiveChallenge")` | `On<ChallengeNotification>("ReceiveChallenge")` | `ChallengeNotification` | Another user challenged you |
| `SendAsync("BattleResolved")` | `On<BattleResult>("BattleResolved")` | `BattleResult` | Battle resolved (to both users) |
| `SendAsync("ChallengeDeclined")` | Handled via Fluxor | `int battleId` | Opponent declined your challenge |
| `SendAsync("ChallengeSent")` | Handled via Fluxor | `int battleId` | Confirmation to sender |

### Connection Lifecycle

```
  Disconnected
      │
      ▼ ConnectAsync()
  Connected ────────────────────────► UpdateLocation(lat,lng)
      │                                   │
      │ network drop                      │
      ▼                                   │
  Reconnecting ── retry [0s,2s,10s,30s]──┤
      │                                   │
      ├── success ────────────────────────► Reconnected
      │   (re-join active battle groups)   (re-send position)
      │
      └── all retries failed ────────────► Closed
                                           (show manual reconnect)
```

### Battle Resolution Flow

```
Challenger                        Server (BattleHub)                    Opponent
    │                                    │                                  │
    │── SendChallenge(opp,playerId) ────►│                                  │
    │                                    │── validates                     │
    │                                    │── persists BattleModel (Pending)│
    │                                    │── Clients.User(opp).SendAsync───►│
    │                                    │   ("ReceiveChallenge", notif)   │
    │◄── ChallengeSent(battleId) ───────│                                  │
    │                                    │                                  │
    │                                    │◄── AcceptChallenge(id,player) ──│
    │                                    │── validates (pending, not self)  │
    │                                    │── BattleResolver.Resolve()      │
    │                                    │── updates DB (Completed)        │
    │                                    │── Clients.User(challenger) ────►│
    │◄── BattleResolved(result) ────────│     .SendAsync("BattleResolved")│
    │                                    │── Clients.Caller ───────────────►│
    │                                    │     .SendAsync("BattleResolved")│
```

---

## 7. FAQ / Gotchas

### Map not showing / blank div

**Problem:** The Leaflet map renders as a grey or blank area.

**Solution:** The map `<div>` must have an explicit CSS height. The Map.razor page uses:

```css
#leafletMap {
    height: calc(100vh - 56px); /* full height minus navbar */
}
```

If the div has `height: 0` (implicit div default), Leaflet cannot calculate its viewport. Inline styles or a dedicated CSS rule are required.

### NTS coordinate confusion

**Problem:** Pitches appear in the wrong location (e.g., middle of the ocean).

**Solution:** Remember the coordinate order:

| Context | X | Y |
|---------|---|---|
| NTS `new Point(x, y)` | Longitude | Latitude |
| Leaflet `[lat, lng]` | Latitude | Longitude |
| Browser Geolocation API | Latitude | Longitude |

Always swap when bridging between Leaflet JS and NTS C#.

### NTS Distance() returns degrees on client

**Problem:** `point1.Distance(point2)` in client-side C# returns values like `0.00123` instead of proper meters.

**Solution:** NTS client-side calculations ignore SRID and return Euclidean distance in the coordinate units (degrees for WGS84). Always use:
- **Server-side** EF Core LINQ queries for distance (SQL Server `STDistance()` returns meters)
- **Client-side** `GeoPosition.HaversineDistance()` from `RuckR.Shared.Models` when server round-trip isn't needed

### GPS permission denied

**Problem:** Browser prompts for location and user denies.

**Solution:** The app gracefully falls back. The `GeolocationService` dispatches a `LocationErrorAction` to Fluxor. The Map page shows the map centered on the default seed location (London, configurable via `Seed.DefaultCenterLat`/`Lng`) and displays a prompt to enable GPS.

### LocalDB not available

**Problem:** `dotnet ef database update` fails with "Cannot connect to (localdb)\MSSQLLocalDB".

**Solution:** LocalDB is Windows-only. On macOS/Linux:

1. Run Docker SQL Server:
   ```sh
   docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Pass" \
     -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
   ```
2. Update the connection string in `appsettings.Development.json`:
   ```json
   "RuckRDbContext": "Server=localhost,1433;Database=RuckR_Dev;User Id=sa;Password=YourStrong!Pass;TrustServerCertificate=True;"
   ```

### SignalR connection drops

**Problem:** SignalR disconnects when the browser tab backgrounds (mobile) or network is flaky.

**Solution:** `WithAutomaticReconnect()` handles retry with exponential backoff: `[0s, 2s, 10s, 30s]`. On `Reconnected`, the client:
1. Dispatches `SetConnectionStateAction(true, null)` to Fluxor
2. Re-joins all active battle groups
3. Re-sends current GPS position via `UpdateLocation`

The `ConnectionStatus.razor` component in the navbar shows the current state (green dot = connected, yellow pulsing = reconnecting, red = disconnected).

### Capture fails with "GPS position required"

**Problem:** `POST /collection/capture` returns `400 Bad Request: GPS position required`.

**Solution:** The capture endpoint requires a **recent server-side GPS position**. The client must:
1. Be connected to SignalR (`/battlehub`)
2. Have sent `UpdateLocation` within the last **60 seconds**
3. Have GPS accuracy < **50 meters**

If the SignalR connection hasn't sent position yet (e.g., just after app load), wait for the first `UpdateLocation` to complete before attempting capture.

### Duplicate capture → 409 Conflict

**Problem:** Attempting to capture a player already in your collection.

**Solution:** Each user can capture a given player only once. The `Collections` table has a unique index on `(UserId, PlayerId)`. The server catches both:
- Pre-check via `AnyAsync()` before insert
- Post-insert `DbUpdateException` (SQL error 2601/2627) as a race-condition safety net

### Challenge concurrency → 409 Conflict

**Problem:** Two users accept the same challenge simultaneously.

**Solution:** The `BattleModel` has a `[Timestamp] RowVersion` concurrency token. If a `DbUpdateConcurrencyException` is thrown, the server returns `409 Conflict` with the message "This challenge was already accepted or modified by another request."

### Rate limiting errors

| Endpoint | Limit | Error code |
|----------|-------|------------|
| `POST /pitches` | 5 per user per day | 429 |
| `POST /collection/capture` | 20 per user per hour | 429 |
| `POST /battles/challenge` | 10 per user per hour | 429 |

Rate limits use in-memory `ConcurrentDictionary<string, List<DateTime>>` with periodic cleanup. Limits reset on server restart (acceptable for MVP).

### Seed data not generated

**Problem:** Players table is empty after running the app.

**Solution:** The `SeedService` runs on app startup but has a try/catch that *skips* seeding if the database isn't available yet. Check the startup logs for:

```
Seed data generation skipped — database may not be available yet.
```

Ensure the database is created (`dotnet ef database update`) before running. If you need to re-seed, delete all rows from `Players` and `Pitches` tables and restart the app.

### Build issues with NetTopologySuite in WASM

**Problem:** `dotnet build` fails for Client project with NTS-related errors.

**Solution:** Only the **core** `NetTopologySuite` package should be in `RuckR.Shared`. The spatial EF Core provider (`Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite`) must only be in `RuckR.Server`. The `NetTopologySuite.IO.SqlServerBytes` package (transitive dependency) is not WASM-compatible and should not leak into the Shared or Client projects.

---

## Appendix: Key File Map

```
RuckR.sln
├── RuckR/Server/
│   ├── Program.cs                          # DI, middleware, SignalR, seed startup
│   ├── appsettings.json                    # Connection string, seed config
│   ├── appsettings.Development.json        # LocalDB connection string
│   ├── Controllers/
│   │   ├── PlayersController.cs            # GET /players, /players/{id}, /players/nearby
│   │   ├── PitchesController.cs            # GET/POST /pitches and /pitches/nearby
│   │   ├── CollectionController.cs         # GET/POST /collection and /collection/{id}/favorite
│   │   ├── BattlesController.cs            # POST /battles/challenge, /accept, /decline; GET /pending, /history
│   │   └── ProfileController.cs            # GET/PUT /profile (in-memory static)
│   ├── Data/
│   │   └── RuckRDbContext.cs               # EF Core DbContext (Identity + game DbSets + spatial config)
│   ├── Hubs/
│   │   └── BattleHub.cs                    # SignalR hub: challenges, location, battle resolution
│   ├── Services/
│   │   ├── BattleResolver.cs               # Stat-based battle resolution engine
│   │   ├── IBattleResolver.cs
│   │   ├── LocationTracker.cs              # In-memory user GPS position store (singleton)
│   │   ├── ILocationTracker.cs
│   │   ├── PlayerGeneratorService.cs       # Seed-based rugby player generation
│   │   └── SeedService.cs                  # Startup check + auto-seed orchestration
│   └── Migrations/
│       └── InitialCreate.cs                # Identity + game tables + spatial indexes
│
├── RuckR/Client/
│   ├── Program.cs                          # Fluxor, SignalRClientService, typed HttpClient
│   ├── App.razor                           # StoreInitializer, Router
│   ├── Pages/
│   │   ├── Map.razor                       # /map — full-screen Leaflet map
│   │   ├── Catalog.razor                   # /catalog — player catalog with filters
│   │   ├── Collection.razor                # /collection — user's collected players
│   │   ├── PlayerGrid.razor                # /players/nearby — nearby players for PVP
│   │   ├── Battle.razor                    # /battle — PVP challenge UI
│   │   ├── BattleHistory.razor             # /battles/history — past battle results
│   │   ├── PitchCreate.razor              # /pitches/create — create a new pitch
│   │   └── Profile.razor                   # /profile — user profile (view/edit)
│   ├── Services/
│   │   ├── ApiClientService.cs             # Typed HttpClient for all REST endpoints
│   │   ├── SignalRClientService.cs         # HubConnection manager (singleton)
│   │   ├── GeolocationService.cs           # Browser geolocation API wrapper (scoped)
│   │   ├── IGeolocationService.cs
│   │   ├── MapService.cs                   # Leaflet JS interop wrapper (scoped)
│   │   └── IMapService.cs
│   ├── Store/
│   │   ├── LocationFeature/                # LocationState, LocationActions, LocationReducers
│   │   ├── MapFeature/                     # MapState, MapActions, MapReducers
│   │   ├── GameFeature/                    # GameState (auth + connection), Actions, Reducers
│   │   ├── InventoryFeature/               # InventoryState (collected players), Actions, Reducers
│   │   └── BattleFeature/                  # BattleState (challenges + history), Actions, Reducers
│   └── wwwroot/js/
│       ├── leaflet-map.module.js           # Leaflet ES module: initMap, markers, pan
│       └── geolocation.module.js           # Geolocation ES module: getCurrentPosition, watch
│
└── RuckR/Shared/
    └── Models/
        ├── PlayerModel.cs                  # Rugby player creature (stats, rarity, position)
        ├── PitchModel.cs                   # Virtual rugby pitch (Point geography)
        ├── CollectionModel.cs              # User's captured players (UserId, PlayerId)
        ├── BattleModel.cs                  # PVP battle state (Pending → Completed)
        ├── GeoPosition.cs                  # GPS DTO + Haversine distance + DistanceBucket
        ├── GameEnums.cs                    # PlayerPosition, PlayerRarity, PitchType, BattleStatus
        ├── PitchFormModel.cs               # Pitch creation form model
        ├── DTOs.cs                         # ChallengeRequest, AcceptChallengeRequest, CreatePitchRequest,
        │                                   # ChallengeNotification, BattleResult, NearbyPlayerDto, CapturePlayerRequest
        └── ProfileModel.cs                 # User profile (legacy)
```
