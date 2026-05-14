# RuckR Implementation Plan — Post-MVP Map Phase

> Generated from canonical spec (`docs/plan/20260511-canonical-ruckr-creature-collector-gps-spec.md`),
> map spec (`docs/plan/map-spec.md`), and current codebase state as of release `20260513163544`.

---

## Current State Summary

**Already shipped (release `20260513163544`, live at ruckr.exe.xyz):**

- [x] GeoBlazor MapView with ArcGIS Navigation basemap + widgets (Home, Compass, Locate, ScaleBar, Zoom)
- [x] API key via `IConfiguration["ArcGISApiKey"]` → `<BasemapStyle ApiKey="@_apiKey" />`
- [x] CSP fixed in `index.html` meta tag + `Server/Program.cs` middleware
- [x] GPS watch → user position marker (blue circle)
- [x] Nearby pitch fetching via `ApiClientService.GetNearbyPitchesAsync()`
- [x] Pitch markers rendered with `MapView.AddGraphic()` colored by `PitchType`
- [x] Pitch click/tap → selection overlay (bottom sheet with name, type, distance bucket, player count)
- [x] Pitch-type filter buttons (🏟 Stadium, ⚽ Standard, 💡 Training) with center-on-nearest
- [x] Null-island (0,0) and sentinel (-1,-1) coordinate guards
- [x] `PitchModel` lat/lng fix: `[NotMapped]` settable properties for JSON serialization
- [x] Service worker `skipWaiting()` + `clients.claim()` for instant activation
- [x] `Cache-Control: no-cache, no-store, must-revalidate` for `/_framework/` paths
- [x] Fluxor state (`MapState`, `MapActions`, `MapReducers`) — reused unchanged
- [x] Playwright e2e suite: 7 tests, all passing
- [x] Deploy script `./scripts/publish-exe-dev.sh --yes` working

**Server-side ready (not yet wired into map UI):**

- [x] `RecruitmentController` — `GET /api/recruitment/profile`, `POST /api/recruitment/attempt`
- [x] `MapController` — `GET /api/map/encounters?lat=&lng=&radius=`
- [x] `RecruitmentService` — encounter generation, proximity validation, success chance calc
- [x] `CollectionController` — `GET /api/collection/capture-eligibility/{pitchId}`
- [x] All DTOs defined in `Shared/Models/DTOs.cs` (`PlayerEncounterDto`, `RecruitmentAttemptResultDto`, `CaptureEligibilityDto`, `GameProgressDto`)
- [x] Recruitment API tests passing

**What's missing (client-side integration + polish):**

- [ ] Encounter markers on map with rarity colors
- [ ] Encounter click → recruit overlay with attempt flow
- [ ] Game progress chip (level/XP bar)
- [ ] Proximity-aware marker glow/pulse animation
- [ ] Onboarding banner (first-time visitors)
- [ ] Map position persistence (localStorage)
- [ ] Browser QA / design review pass
- [ ] Additional e2e tests for encounters and recruitment

---

## Phase 1: Wire Up Encounters + Recruitment Flow (P0)

**Goal:** Players can see encounter markers, tap them, and attempt recruitment from the map.

### 1.1 — Fetch Nearby Encounters

**File:** `RuckR/Client/Services/ApiClientService.cs` (line 120)

- `GetEncountersAsync(double lat, double lng, double radius = 300)` already exists in `ApiClientService`
- Verify it calls `GET api/map/encounters?lat={}&lng={}&radius={}`
- Ensure `PlayerEncounterDto` is properly deserialized (check `DTOs.cs` line 53)

**Action:** No code change needed — verify end-to-end by hitting the endpoint manually or in test.

### 1.2 — Encounter Fluxor State

**Files:**
- `RuckR/Client/Store/MapFeature/MapState.cs` — add `SelectedEncounterId` property
- `RuckR/Client/Store/MapFeature/MapActions.cs` — add `SelectEncounterAction`, `ClearEncounterAction`
- `RuckR/Client/Store/MapFeature/MapReducers.cs` — add reducers for the new actions

### 1.3 — Render Encounter Markers on Map

**File:** `RuckR/Client/Pages/Map.razor`

- Fetch encounters alongside pitches in `OnViewInitialized` (or when GPS updates):
  ```csharp
  var encounters = await ApiClient.GetEncountersAsync(position.Latitude, position.Longitude, 3000);
  ```
- Render with `MapView.AddGraphic()` using rarity-based `SimpleMarkerSymbol`:

  | Rarity     | Color      | Hex       |
  |------------|------------|-----------|
  | Common     | Gray       | `#6B7280` |
  | Uncommon   | Green      | `#2F855A` |
  | Rare       | Blue       | `#2563EB` |
  | Epic       | Amber      | `#D97706` |
  | Legendary  | Red        | `#C81E1E` |

- Track encounter graphics in `Dictionary<Guid, Graphic>` keyed by encounter ID
- Use a **different symbol shape** (e.g., diamond or star) to distinguish encounters from pitch markers

### 1.4 — Encounter Click → Recruit Overlay

**File:** `RuckR/Client/Pages/Map.razor`

- Update `MapView.HitTest()` logic to differentiate pitch vs. encounter hits:
  - Check `Graphic.Attributes["_ruckrType"]` or similar discriminator
  - If encounter: open recruit overlay instead of pitch info sheet
- Overlay should display:
  - Player name, rarity badge, level
  - Distance bucket
  - **"Recruit" button** — enabled only if:
    - GPS accuracy ≤ 50m
    - Player within 100m of encounter position (client-side pre-check)
    - Encounter not expired (check `ExpiresAtUtc`)
  - Success chance percentage (from `PlayerEncounterDto.SuccessChance`)

### 1.5 — Attempt Recruitment

**File:** `RuckR/Client/Pages/Map.razor` + `ApiClientService.cs`

- Call `ApiClient.AttemptRecruitmentAsync(encounterId, playerId)`
- Handle results:
  - **Success:** Add to local collection state, show success toast, remove encounter marker
  - **Failure (expired/moved/failed roll):** Show reason message, refresh encounters
- After recruitment, refresh both encounters and collection

### 1.6 — Pitch Capture Eligibility Integration

**File:** `RuckR/Client/Pages/Map.razor`

- When a pitch is selected, call `ApiClient.GetCaptureEligibilityAsync(pitchId)` to get server-side eligibility
- Enable the "Capture Player" button only when:
  - `CaptureEligibilityDto.IsEligible` is true
  - GPS accuracy ≤ 50m
  - Player within 100m (client-side distance check using pitch lat/lng)
- Show the reason for ineligibility (e.g., "Too far from pitch", "GPS accuracy too low")

---

## Phase 2: Polish & UX Improvements (P1)

### 2.1 — Game Progress Chip

**Files:**
- `RuckR/Client/Pages/Map.razor` — add a compact chip in top-right corner
- `ApiClientService.cs` — `GetGameProgressAsync()` already exists (line 149)
- Display: `Level X` + XP progress bar (experience / nextLevelExperience)
- Consider dispatching to Fluxor `GameState` on load

### 2.2 — Proximity-Aware Marker Glow

**File:** `RuckR/Client/Pages/Map.razor`

- Pitches within 100m of player → add pulsing glow effect
- Approach: periodically recalculate distance and toggle a CSS class or update symbol opacity
- Use `MapView.UpdateGraphic()` or remove/re-add graphics with updated symbol

### 2.3 — Onboarding Banner

**File:** `RuckR/Client/Pages/Map.razor` + `localStorage`

- First-time visitors see a dismissible banner: "Welcome to RuckR! Explore the map to find pitches and recruit players."
- Store `onboarded = true` in `localStorage`
- Show only if `localStorage.getItem('ruckr_onboarded')` is null

### 2.4 — Map Position Persistence

**File:** `RuckR/Client/Pages/Map.razor`

- On map moveend, save `center` and `zoom` to `localStorage`
- On load, restore if GPS is unavailable; otherwise use GPS position
- Keys: `ruckr_map_lat`, `ruckr_map_lng`, `ruckr_map_zoom`

---

## Phase 3: Hardening & Tests (P2)

### 3.1 — Encounter-Specific E2E Tests

- Test: Encounter markers appear within radius of player
- Test: Tapping encounter opens recruit overlay
- Test: Recruit button disabled when GPS accuracy is low
- Test: Successful recruitment removes encounter and updates collection
- Test: Expired encounter shows "expired" state

### 3.2 — Server-Side Validation Tests

- Distance bucketing accuracy (< 50m, < 100m, < 250m, < 500m, > 500m)
- Stale location rejection (>60s old)
- Poor accuracy rejection (>50m)
- Already-collected player rejection
- Encounter expiry validation (5-minute TTL)

### 3.3 — SignalR Integration (P2 nice-to-have)

- Wire `BattleHub` for real-time pitch discovery notifications
- Client joins a SignalR group on map load
- Broadcast new pitch/encounter creation events to nearby players
- Handle disconnect/reconnect with state replay

### 3.4 — Browser QA Pass

- Run gstack/Playwright against deployed app
- Screenshot all key states: GPS allowed, GPS denied, pitch selected, encounter selected, recruit success, recruit failure, loading, disconnected
- Validate mobile layout (iOS Safari, Chrome Android)
- Validate desktop layout (Chrome, Firefox)

---

## Technical Constraints (Reiterated)

| Constraint | Detail |
|---|---|
| Graphic rendering | `MapView.AddGraphic()` only — no `<GraphicsLayer>` (GeoBlazor 4.4.4 bug) |
| MapColor alpha | 0.0–1.0, not 0–255 |
| Size type | `Dimension` required for symbol sizes |
| Attributes | `AttributesDictionary` takes `Dictionary<string, object>` |
| GPS permissions | Both `Permissions: ['geolocation']` context permission AND `navigator.geolocation` prompt |
| ArcGIS key | From `IConfiguration["ArcGISApiKey"]`, sourced from `ARC_GIS_API_KEY` env var |
| CSP | `https://js.arcgis.com` in `script-src` AND `font-src` (two places) |
| Deployment | `./scripts/publish-exe-dev.sh --yes` |
| State management | Fluxor only (no manual `CascadingValue` for game state) |

---

## Acceptance Criteria

**Phase 1 done when:**
- [ ] Encounter markers render on map within 5km radius, colored by rarity
- [ ] Tapping an encounter opens a bottom sheet with player info + recruit button
- [ ] Recruit button disabled when GPS accuracy > 50m or distance > 100m
- [ ] Successful recruitment adds player to collection and removes encounter marker
- [ ] Failed recruitment shows reason (expired, already collected, distance, accuracy)
- [ ] Pitch selection overlay shows server-side capture eligibility status
- [ ] All new API calls handle errors gracefully with retry banners

**Phase 2 done when:**
- [ ] Level/XP chip visible in top-right, updating after recruitment
- [ ] Nearby pitches (100m) pulse/glow to indicate discoverability
- [ ] First-time visitors see onboarding banner (dismissible, localStorage-backed)
- [ ] Map restores last position on revisit when GPS is unavailable

**Phase 3 done when:**
- [ ] Encounter e2e tests passing in CI
- [ ] Server-side validation tests covering all rejection cases
- [ ] Browser QA screenshots documented for all key states
- [ ] No overlapping controls on mobile (375px width) or desktop

---

## Risk Register

| Risk | Likelihood | Mitigation |
|---|---|---|
| GeoBlazor 4.4.4 `AddGraphic` rendering issues on mobile Safari | Medium | Test early with BrowserStack; fallback to `ArcGISGraphicsModule` JS interop |
| GPS accuracy varies wildly on Android Chrome | High | Use 60s staleness window + 50m accuracy gate; show clear UX messages |
| Encounter expiry (5 min) causes stale markers | Medium | Cleanup expired encounters on every `GetEncountersAsync` call (already done server-side) |
| SignalR reconnection race conditions | Low (deferred) | Client queue already exists; add server idempotency only when needed |
| Fluxor state desync between map and collection pages | Medium | Ensure `Dispose()` cleans up subscriptions; use `NavigationCacheMode.Required` |

---

## Estimated Effort

| Task | Estimate |
|---|---|
| Phase 1: Encounter markers + recruitment flow | 2–3 days |
| Phase 2: Progress chip + proximity glow + onboarding | 1–2 days |
| Phase 3: Tests + browser QA + hardening | 1–2 days |
| **Total** | **4–7 days** |