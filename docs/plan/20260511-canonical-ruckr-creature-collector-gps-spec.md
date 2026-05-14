# Canonical RuckR Creature Collector GPS Spec

Date: 2026-05-11
Status: canonical planning target
Synthesizes:

- `docs/plan/20260505-creature-collector-rugby-gps/PRD.yaml`
- `docs/plan/20260505-creature-collector-rugby-gps/plan.yaml`
- `docs/plan/20260505-creature-collector-rugby-gps/map-design-and-implementation-plan.md`
- `docs/plan/20260509-leaflet-to-geoblazor-migration/plan.yaml`
- `docs/plan/20260510-dotnet10-jsinterop-action-plan.md`
- `docs/plan/20260510-mssql-in-memory-replacement.md`
- `docs/handoff-report-2026-05-11.md`

## Product Direction

RuckR is a browser-based rugby creature-collector GPS game. Players register, explore a real-world map, discover virtual rugby pitches, recruit fictional rugby players, build a collection, and challenge other players in asynchronous stat-based PVP battles.

The first playable experience should be the map, not a marketing or tutorial page. The player opens the app, sees where they are, sees nearby rugby activity, and has a clear next action even if GPS is denied or temporarily unavailable.

## Canonical Scope

In scope:

- ASP.NET Core Identity registration, login, logout, and authenticated session state.
- SQL Server persistence through EF Core with NetTopologySuite spatial support.
- Seeded fictional rugby players and pitches, plus user-created or discovery-created pitch expansion.
- GeoBlazor/ArcGIS map UI showing the player, pitches, encounters, distance buckets, and capture/recruit actions.
- Browser geolocation through focused JS interop.
- Fluxor state for cross-page game, location, inventory, battle, and map state.
- SignalR for location updates, challenge notifications, battle updates, and pitch discovery notifications.
- Offline-aware SignalR client behavior: bounded outbound queue, coalesced location updates, replay after reconnect, browser online/offline state, and connection quality display.
- Async PVP challenges with server-side validation, idempotency, rate limits, expiry, and persisted battle history.
- Production deploy to exe.dev with local backup copy to `/mnt/c/Users/clock/dbbackups`.
- Local Docker SQL Server restore workflow for inspecting production backups.

Out of scope for the next implementation cycle:

- Native mobile apps.
- Real-money purchases.
- Third-party auth providers.
- Trading, gifting, global chat, friends, or leaderboards.
- Synchronous real-time battles.
- Automated matchmaking.
- Redis or any external backplane unless the deployment actually becomes multi-instance.
- A full offline data store. The app should queue transient SignalR actions, not become offline-first.

## Superseded Decisions

The original PRD and map plan specified Leaflet. That is now superseded. The canonical map renderer is GeoBlazor using ArcGIS MapView/WebMap, with environment-sourced ArcGIS and GeoBlazor configuration.

The GeoBlazor migration plan originally aimed for no custom JS modules. That is now too strict. The canonical rule is: use GeoBlazor for map ownership, and keep small targeted JS modules when they solve browser APIs or GeoBlazor gaps. Current accepted modules include:

- `geolocation.module.js` for GPS and browser online/offline events.
- `arcgis-graphics.module.js` for direct ArcGIS graphics management where GeoBlazor Core 4.4.4 is not sufficient.
- `browser-logging.module.js` for client-side telemetry.

The SQL Server in-memory replacement plan should not be read as "immediately remove every in-memory service." The canonical rule is:

- SQL Server is the durable system of record.
- In-memory services are allowed for ephemeral single-instance MVP state.
- Move location presence, rate windows, and SignalR scale-out into SQL only when durability, restart recovery, or multi-instance deployment requires it.

## Current Implementation Baseline

Already implemented:

- .NET 10 across Server, Client, and Shared.
- EF Core SQL Server + NetTopologySuite.
- ASP.NET Core Identity with EF-backed users.
- `RuckRDbContext` with players, pitches, collection, battles, user game profile, encounters, rate limits, consent, and profile entities.
- Seed service and player generator.
- Server-side controllers for players, pitches, collection, battles, recruitment, privacy, telemetry, map, and profile.
- SignalR `BattleHub` with authenticated location updates, pitch discovery callbacks, challenges, accept/decline flow, idempotency key support, and latency `Ping`.
- Fluxor state for game, battle, inventory, location, and map.
- GeoBlazor map page at `/map` and `/`.
- Browser geolocation service and JS module.
- Connection status component with online/offline state, queue count, and latency quality.
- SignalR offline action queue with bounded replay and coalesced location updates.
- exe.dev deployment script with production backup copied locally to `/mnt/c/Users/clock/dbbackups`.
- Local Docker SQL Server restore target for backup inspection.

Known implementation gaps:

- Map code still contains visual copy and controls inherited from earlier iterations and should get a design pass against the canonical map UX.
- The map depends on ArcGIS/GeoBlazor configuration; the app needs an explicit user-facing fallback when those values are absent.
- Some original acceptance criteria mention Leaflet and OpenStreetMap; those should be deleted or rewritten in future docs.
- SignalR queue behavior is client-side only; server replay/idempotency coverage is strongest for challenges, weaker for every possible hub action.
- `ILocationTracker` remains in-memory. This is acceptable for one app instance, but not for multi-instance scale-out or restart-resilient live presence.
- GeoBlazor + direct ArcGIS graphics needs browser QA because compile-time tests cannot catch all rendering failures.

## Target User Experience

Default route:

- The root route loads the map.
- If GPS is available, the map centers on the player and shows a distinct player marker.
- If GPS is denied, unavailable, or inaccurate, the map remains browsable and explains only the next useful action.
- Nearby pitch and encounter markers are shown within the browse radius.
- Selecting a pitch opens a bottom sheet or compact overlay with pitch name, pitch type, fuzzy distance bucket, available player count, and contextual actions.
- Capture/recruit actions are enabled only when server-side eligibility passes.
- Connection status is visible but compact: connected/reconnecting/disconnected, browser online/offline, quality, and queued action count.

Distance and privacy:

- Exact player GPS is never shown to other users.
- UI uses buckets such as `< 50m`, `< 100m`, `< 250m`, and `< 500m`.
- Server-side capture/recruit validation uses recent location, acceptable accuracy, and spatial distance.

## Architecture

Server:

- ASP.NET Core hosts APIs, Identity UI, SignalR, migrations, seeding, and Blazor WASM assets.
- SQL Server is the durable store.
- NetTopologySuite/SQL Server geography supports pitch and spawn proximity queries.
- SignalR hub methods are validation boundaries, not trusted client command pipes.
- Rate limits and battle state are persisted.

Client:

- Blazor WASM served by the Server project.
- GeoBlazor owns map rendering.
- Focused JS modules cover browser APIs and ArcGIS graphics gaps.
- Fluxor owns shared reactive state.
- `SignalRClientService` owns hub connection lifecycle, reconnect, queue/replay, and latency sampling.

Operations:

- `scripts/publish-exe-dev.sh` is the canonical deploy path.
- Production backup must be copied locally to `/mnt/c/Users/clock/dbbackups`.
- Restored inspection database should run in a separate Docker SQL Server container and database name, currently `RuckR_Inspect`.

## Data Model Direction

Core entities:

- `PlayerModel`: fictional rugby player creature, position, rarity, stats, bio, spawn metadata.
- `PitchModel`: geospatial rugby pitch, type, creator, location, generated or user-created origin.
- `CollectionModel`: permanent user inventory.
- `PlayerEncounterModel`: time-bounded recruitable encounter for a user/player.
- `BattleModel`: pending/completed/declined/expired async PVP challenge.
- `UserGameProfileModel`: level, experience, progression state.
- `RateLimitRecord`: durable action rate limiting.
- `UserConsent`: location/privacy consent state.

Durability rule:

- Anything that affects player progress, fairness, or battle outcomes must be persisted.
- Live presence can remain ephemeral until multi-instance hosting or restart recovery becomes a product requirement.

## Implementation Roadmap

### Phase 1: Stabilize The Current Playable Map

- Audit `/map` against the canonical UX.
- Ensure missing ArcGIS config produces a useful fallback state.
- Verify marker rendering, pitch selection, encounter selection, recruit action, and center controls in a real browser.
- Remove stale Leaflet mental model from map acceptance criteria.

Acceptance:

- Map loads at `/` and `/map`.
- GPS denied still leaves a usable map.
- Pitch/encounter selection works on desktop and mobile widths.
- No overlapping controls or unreadable button text.

### Phase 2: Finish Capture And Recruitment Confidence

- Confirm server-side capture/recruit eligibility uses recent location and accuracy.
- Add/verify tests for distance buckets, stale location, poor accuracy, already-collected cases, and encounter expiry.
- Make map feedback explain why capture/recruit is disabled.

Acceptance:

- Successful recruit updates collection and progression.
- Ineligible recruit attempts fail server-side even if the client is manipulated.
- Collection and map state update after recruit without a full page refresh.

### Phase 3: Harden Realtime And Offline Behavior

- Keep the current SignalR offline queue.
- Add server idempotency where queued/retried actions can mutate durable state.
- Verify reconnect replays latest location and rejoins battle groups.
- Add focused tests for queued location coalescing, challenge idempotency, and browser offline state.

Acceptance:

- Turning network off queues a location update instead of dropping app state.
- Reconnect replays queued actions once.
- Duplicate challenge retries do not create duplicate battles.

### Phase 4: Decide SQL-Backed Presence Only When Needed

- Keep `LocationTracker` in-memory for the single-instance MVP.
- Before adding SQL-backed presence, define the exact requirement: multi-instance SignalR, restart recovery, auditability, or anti-spoofing.
- If needed, add a short-lived `PlayerLocation` table with cleanup and indexes; do not introduce Redis for the current exe.dev shape.

Acceptance:

- Single-instance app remains simple.
- Scale-out work has a concrete trigger and test plan.

### Phase 5: Browser QA And Design Review

- Run gstack/browser or Playwright against the deployed and local app.
- Validate mobile and desktop layout.
- Screenshot GPS denied, loading, pitch selected, encounter selected, disconnected, reconnecting, and queued-action states.
- Fix visible layout and hierarchy issues before adding more features.

Acceptance:

- Browser QA has screenshots or explicit evidence.
- Map controls do not overlap.
- Connection status remains compact.

### Phase 6: Operational Guardrails

- Keep deploy backup copy local.
- Document local restore command and expected database counts.
- Add a smoke check after deploy that verifies health, map appsettings injection, and a recent backup file.

Acceptance:

- Deploy reports release ID, health result, backup file, and local backup path.
- Local inspection container can restore the latest backup without touching production.

## Autoplan Review

### CEO Review

Verdict: hold the core scope, but stop expanding systems work until the map loop feels real.

The 10-star product is not "more infrastructure"; it is opening the app and immediately understanding the nearby rugby world. The best next slice is map/recruit confidence with offline/reconnect polish already in place. SQL-backed presence and SignalR backplanes are valuable later, but premature for a single VM unless we explicitly commit to multi-instance hosting.

Decision:

- Prioritize playable map loop over further platform rewrites.
- Keep PVP async and simple.
- Defer social systems, matchmaking, and trading.

### Design Review

Verdict: the map UX needs a focused polish pass before new feature work.

The product plan is strong, but the current UI has accumulated controls from multiple waves. The canonical design should be dense, game-like, and operationally clear: map first, compact status, bottom sheet for selected entities, minimal explanatory copy, strong disabled-action feedback.

Decision:

- Treat the map page as the primary product surface.
- Replace verbose in-app instruction text with stateful controls and concise action feedback.
- Verify mobile layout with screenshots before shipping more map features.

### Engineering Review

Verdict: architecture is coherent if we explicitly accept hybrid GeoBlazor plus targeted JS.

The major risk is stale docs pulling implementation back toward Leaflet or toward unnecessary distributed infrastructure. Current code has a good .NET/SQL/Identity/SignalR spine. The next engineering work should close correctness gaps around map rendering, recruit validation, replay idempotency, and browser QA.

Decision:

- GeoBlazor/ArcGIS is canonical.
- SQL Server remains canonical.
- Client offline queue is canonical.
- Server-side idempotency is required for queued durable mutations.
- Redis/backplane work is deferred.

### DX Review

Verdict: deploy and inspection workflow improved, but should be documented as a first-class operational path.

The local backup copy and Docker restore path remove a lot of risk. The next DX improvement is making post-deploy output deterministic: health, backup filename, local path, and optional restore command.

Decision:

- Keep `scripts/publish-exe-dev.sh` as the blessed deploy path.
- Add docs or script output for local backup/restore.
- Prefer reproducible smoke checks over manual inspection.

## Final Decisions

- Canonical renderer: GeoBlazor/ArcGIS, not Leaflet.
- Canonical persistence: SQL Server with EF Core and NetTopologySuite.
- Canonical realtime: SignalR with client queue/replay; no Redis until multi-instance.
- Canonical map route: `/` and `/map`.
- Canonical immediate product slice: stabilize the playable map/recruit loop.
- Canonical ops requirement: production backup copied to `/mnt/c/Users/clock/dbbackups` and restorable locally.

## Next Recommended Work Item

Run a focused map QA/design pass:

1. Start the app locally against the restored or dev SQL Server.
2. Exercise `/map` with GPS allowed, GPS denied, offline, reconnecting, pitch selected, encounter selected, and recruit success.
3. Fix layout/state bugs found.
4. Add targeted tests for recruit eligibility and SignalR replay/idempotency.

