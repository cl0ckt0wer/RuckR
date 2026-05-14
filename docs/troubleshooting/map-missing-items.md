# Troubleshooting: Map Not Showing Players, Pitches, etc.

## Overview

The map rendering pipeline has 5 layers: **Database → API → Client Services → Fluxor State → ArcGIS JS Interop → MapView**. A break at any layer causes items to silently disappear (errors are swallowed in `BootstrapMapDataAsync`).

---

## 1. Pitches Not Showing

### 1.1 — Empty API Response
- **Check:** Open browser DevTools → Network tab → `GET /api/pitches/nearby?lat=...&lng=...&radius=500`
- **If 0 results:** The spatial query `IsWithinDistance` may be failing.
  - Verify SQL Server spatial index exists on `Pitches.Location`
  - Verify SRID is 4326 (mismatch = silent empty results)
  - Run manual query: `SELECT * FROM Pitches WHERE Location.STDistance(...) IS NOT NULL`
- **If 404/500:** Controller routing issue — check `[Route("[controller]")]` on `PitchesController`

### 1.2 — JS Module Failed to Load
- `Map.razor` returns early if `_graphicsModule` is null
- **Check:** Console for `import("./js/arcgis-graphics.module.js")` errors
- **Fix:** Ensure `arcgis-graphics.module.js` is in `wwwroot/js/` and served by static files middleware

### 1.3 — ArcGIS API / Portal Item Not Configured
- `Map.razor` sets `_hasError = true` if `ArcGISApiKey` or `ArcGISPortalItemId` is missing/empty
- **Check:** Look for the "Failed to load map" error banner; inspect `appsettings.json` or `app.env` for blank values
- **Fix:** Set `ArcGISApiKey` and `ArcGISPortalItemId` in environment variables / `app.env` on the server

### 1.4 — MapView Not Found by JS Interop
- `getMapViewAsync()` polls for up to 15 seconds for the `.esri-view-surface` DOM element
- **Check:** Console for "Timed out waiting for ArcGIS MapView"
- **Fix:** Ensure GeoBlazor's `MapView` component rendered without errors; check for `InvalidChildElementException` in prior builds

### 1.5 — No Seed Data
- `SeedService` only seeds when `Players` table is empty
- Default pitch is at London (51.5074, -0.1278)
- **If testing elsewhere:** No pitches within 500m radius
- **Fix:** Either travel to London, reduce radius, or add test pitches via API

---

## 2. Players / Encounters Not Showing

### 2.1 — Authentication Required (Most Likely Cause)
- `/api/players/nearby` and `/api/map/encounters` both require `[Authorize]`
- **Check:** Are you logged in? Network tab shows 401 → empty list (silently caught)
- **Fix:** Log in first. Check `ApiClientService` auth headers.

### 2.2 — Players Without SpawnLocation
- `PlayersController.GetNearbyPlayers` filters `p.SpawnLocation != null`
- **Check:** Query DB: `SELECT * FROM Players WHERE SpawnLocation IS NULL`
- **Fix:** Seed or update player records with valid `SpawnLocation`

### 2.3 — Recruitment Service Not Returning Data
- Encounters come from `IRecruitmentService.GetEncountersAsync()` (not directly from DB)
- **Check:** Debug or log inside `RecruitmentService` to verify it returns results
- **Fix:** Ensure service is registered in DI and has no internal errors

### 2.4 — DTO Mapping Issues
- `NearbyPlayerDto` and `PlayerEncounterDto` are separate from domain models
- **Check:** Mapster/AutoMapper profiles — any missing mappings?
- **Fix:** Verify `PlayerModel → NearbyPlayerDto` and `PlayerEncounterModel → PlayerEncounterDto` mappings exist

---

## 3. GPS / Location Issues

### 3.1 — Null-Island Filter
- Positions within 0.05° of (0,0) are discarded
- **Symptom:** User marker never appears
- **Fix:** Move away from null-island; check GPS hardware

### 3.2 — Accuracy Too Poor (>200m)
- Positions rejected entirely if `accuracy > 200`
- **Symptom:** User marker stuck at initial position
- **Fix:** Test indoors → go outdoors for better GPS signal

### 3.3 — EMA Smoothing Suppressing Movement
- Exponential moving average smooths out position jumps
- **Symptom:** Map doesn't recenter when you move
- **Fix:** First position is always accepted; subsequent need enough delta to overcome EMA

### 3.4 — Throttle (5s / 20m)
- Updates limited to every 5 seconds OR 20 meters
- **Symptom:** Laggy position updates
- **Fix:** Adjust throttle values in `GeolocationService.ts`

---

## 4. Markers Not Appearing Even Though Data Loaded

### 4.1 — Fluxor State Not Updating
- `SetPitchesAction` / `SetEncountersAction` must be dispatched
- **Check:** Redux DevTools (if configured) or add logging in `MapReducers`
- **Fix:** Verify Fluxor assembly scanning picks up `MapState` (check `[FeatureState]` attribute)

### 4.2 — Graphics Module Null
- If `import("./js/arcgis-graphics.module.js")` fails, `_graphicsModule` stays null
- **Symptom:** Map renders but no pitch/encounter/user markers appear; no JS errors if `catch` block is silent
- **Fix:** Check browser console for CORS or 404 errors loading the module

### 4.3 — Map Container Invisible
- `<div>` with `[data-testid='map-container']` gets class `invisible` when `_isLoading` or `_hasError`
- **Symptom:** Grey/blank area instead of map
- **Check:** Inspect element — if `invisible` class is present, initialization failed
- **Fix:** Check JS console for errors during initialization

### 4.4 — Fire-and-Forget Swallowed Errors
- `BootstrapMapDataAsync()` is called with `_ = ` — exceptions are silently caught
- **Check:** Wrap in try/catch with logging, or check server logs for API errors
- **Fix:** Add proper error handling and retry logic

---

## 5. Diagnostic Checklist

| Step | Action | Expected |
|------|--------|----------|
| 1 | Open browser console | No JS errors |
| 2 | Network tab → filter `api/pitches/nearby` | HTTP 200 with JSON data |
| 3 | Network tab → filter `api/players/nearby` | HTTP 200 (requires auth) |
| 4 | Check `MapState` in Fluxor devtools | `VisiblePitches` array populated |
| 5 | Inspect `[data-testid='map-container']` DOM element | No `invisible` class |
| 6 | Check ArcGIS graphics in DevTools | Graphics present on `view.graphics` |
| 7 | GPS test: check `LocationState` | Valid lat/lng, accuracy < 200 |
| 8 | Verify `arcgis-graphics.module.js` loaded | HTTP 200 in Network tab for the module |

---

## 6. Quick Fixes to Try First

1. **Hard refresh** (Ctrl+Shift+R) — stale JS module cache
2. **Log in** — encounters/players require auth
3. **Zoom out** — markers may exist but off current viewport
4. **Check DB** — run `SELECT COUNT(*) FROM Pitches` and `SELECT COUNT(*) FROM Players`
5. **Check GPS** — ensure browser has location permission
6. **Check map keys** — verify `ArcGISApiKey` and `ArcGISPortalItemId` are set in `app.env`