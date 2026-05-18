# Map Feature Migration Plan

Date: 2026-05-18

## Progress

| Phase | Status | Notes |
|---|---|---|
| Phase 1: Stabilize Diagnostics Noise | Complete locally | Added cancellation/disposal handling for delayed diagnostics in `GameMap` and `DebugMap`; local build passed. Deploy/Jaeger verification pending. |
| Phase 2: Research Widget Replacement | Complete locally | GeoBlazor Core 4.4.4 exposes legacy widget wrappers, not direct ArcGIS web-component replacements. Kept widgets disabled by default and documented as compatibility/debug only. |
| Phase 3: Reduce Graphic Sync Plumbing | Complete locally | Extracted `MapGraphicFactory` and `GraphicsLayerSync`; `GameMap` now owns map decisions while helpers own graphic construction and layer replacement mechanics. Local build passed. |
| Phase 4: Reduce Selection Adapter | Pending | Not started. |
| Phase 5: Decide Popup Policy | Pending | Not started. |

## Goal

Reduce custom map infrastructure now that the Pixel map issue is resolved and the GeoBlazor baseline is proven. The target is not "everything must be GeoBlazor." The target is: use GeoBlazor or ArcGIS component APIs for map behavior, keep RuckR-specific game rules in RuckR code, and remove code that acts like a framework around GeoBlazor.

## Current Map Surface

Primary files:

- `RuckR/Client/Pages/GameMap.razor`
- `RuckR/Client/Pages/DebugMap.razor`
- `RuckR/Client/Pages/DebugLayers.razor`
- `RuckR/Client/wwwroot/js/map-diagnostics.module.js`
- `RuckR/Client/Services/GeolocationService.cs`
- `RuckR/Client/Store/MapFeature/*`
- `RuckR/Client/Store/LocationFeature/*`

Supporting server files:

- `RuckR/Server/Controllers/PitchesController.cs`
- `RuckR/Server/Controllers/MapController.cs`
- `RuckR/Server/Controllers/TelemetryController.cs`
- `RuckR/Server/Hubs/BattleHub.cs`

## Feature Migration Matrix

| Feature | Current implementation | Migration need | Target | Notes |
|---|---|---:|---|---|
| Basemap rendering | `MapView` + `Map` + `BasemapStyle` | No | Keep GeoBlazor | Already native and verified on Pixel. |
| Empty/styled basemap flag | Config/query parsing in `GameMap` | No | Keep app-specific | This is a debug/reduction switch, not a GeoBlazor concern. |
| Map sizing | `MapView.Style` plus app CSS shell | Partial | Keep minimal CSS, avoid ArcGIS internals | Keep explicit host dimensions; do not style GeoBlazor internal DOM. |
| Map loading overlay | `_viewReady` and custom spinner | Partial | Keep app overlay, simplify lifecycle | App-specific UX, but lifecycle should be reduced to `OnViewInitialized` and timeout fallback only. |
| ArcGIS widgets | GeoBlazor `HomeWidget`, `CompassWidget`, `LocateWidget`, `ScaleBarWidget` | Yes | Prefer ArcGIS web components if GeoBlazor exposes them | Current widgets work but produce deprecation warnings from ArcGIS JS. Research GeoBlazor support for `<arcgis-home>`, `<arcgis-locate>`, `<arcgis-scale-bar>`, or component equivalents. |
| Home viewpoint | Custom `HomeViewpoint` calculation | Maybe | GeoBlazor widget config or app helper | Keep only if widget requires `Viewpoint`; otherwise reduce. |
| Center on nearest pitch buttons | Custom overlay buttons calling `_mapView.GoTo` | No | Keep app-specific | Game shortcut behavior belongs in RuckR. Could move out of `GameMap` into a child component later. |
| GPS status chip | Custom UI from `LocationState` | No | Keep app-specific | This is product UX, not a map framework feature. |
| GPS notice/retry | Custom UI + `GeolocationService` | No | Keep app-specific | Browser permission and game eligibility behavior stays custom. |
| Browser geolocation watch | `geolocation.module.js` via `GeolocationService` | No for GeoBlazor | Keep browser API service | GeoBlazor `LocateWidget` is user-triggered map location, not a replacement for game GPS state and server fetches. |
| Player location marker | GeoBlazor `GraphicsLayer` + `Graphic` | Partial | Keep GeoBlazor graphics; reduce update code | Rendering is native, but update/clear/add flow can be simplified into a reusable graphic sync helper. |
| Pitch markers | Three `GraphicsLayer`s with `Graphic`s by pitch type | Partial | Keep GeoBlazor graphics; reduce sync code | Good use of GeoBlazor. Candidate for extracting marker creation/sync from `GameMap`. |
| Encounter markers | `GraphicsLayer` with rarity-colored `Graphic`s | Partial | Keep GeoBlazor graphics; reduce sync code | Same as pitch markers. |
| Candidate place markers | `GraphicsLayer` with visibility toggle | Partial | Keep GeoBlazor graphics; reduce sync code | Visibility is native; candidate create UI is app-specific. |
| Marker symbols | `SimpleMarkerSymbol` factory methods | Maybe | Keep unless GeoBlazor supports better symbol reuse | Current symbols are simple and native. Extract to a `MapSymbols` helper if `GameMap` remains too large. |
| Popup templates | `PopupTemplate` factories | Maybe | Keep GeoBlazor-native | Already native. Decide whether popups are actually needed if RuckR overlays are the primary detail UI. |
| Marker selection | `MapView.OnClick` + clicked `Graphic.Attributes` parsing | Partial | Prefer GeoBlazor hit-test/event model | This is a strong migration candidate. Research whether GeoBlazor exposes graphic click/hit-test results more directly so `GameMap` can avoid manual attribute adapters. |
| Pitch overlay | Custom bottom-sheet/card UI | No | Keep app-specific | Product/game UX. Not a GeoBlazor feature. |
| Encounter overlay | Custom bottom-sheet/card UI | No | Keep app-specific | Product/game UX. |
| Candidate place overlay | Custom bottom-sheet/card UI | No | Keep app-specific | Product/game UX. |
| Capture eligibility | API call + proximity logic | No | Keep app/server-specific | Domain rule; GeoBlazor should not own this. |
| Recruitment eligibility | Distance/accuracy rules + API call | No | Keep app/server-specific | Domain rule. |
| Game progress chip | API call + custom UI | No | Keep app-specific | Not a map migration target. |
| Onboarding banner | Local storage + custom UI | No for GeoBlazor | Keep app-specific | Could migrate JS localStorage access later, but not to GeoBlazor. |
| Pitch/encounter data fetching | `ApiClientService` + server endpoints | No | Keep app-specific | GeoBlazor renders data; it should not own game data loading. |
| SignalR pitch discovery | `SignalRClientService` events | No | Keep app-specific | Real-time game data belongs outside map rendering. |
| Map state store | Fluxor `MapState` | No | Keep app-specific | Shared app state. Could reduce selected-item duplication later. |
| Location state store | Fluxor `LocationState` | No | Keep app-specific | Shared app state. |
| Map diagnostics | JS DOM/ArcGIS health probe + telemetry | No | Keep temporary/support code | Not a GeoBlazor migration target. Keep lightweight health; remove verbose probes when stable. |
| Debug map | Minimal GeoBlazor sample-style page | No | Keep as regression fixture | Useful for proving GeoBlazor independently from GameMap. |
| Debug layers page | Static links to feature flags | No | Keep while testing | Later hide with `Map:ShowDebugNav=false`; keep route if useful. |
| `/jaeger` access | Disabled public proxy, SSH tunnel only | No | Keep locked down | Operational/security concern, not map feature work. |

## Migration Categories

### Already Good

These should not be reworked unless they regress:

- `MapView` with ArcGIS Navigation basemap.
- Explicit `MapView.Style` sizing.
- GeoBlazor `GraphicsLayer` ownership for pitches, encounters, candidate places, and player location.
- Debug baseline map and debug layer links.
- Feature flags for isolating basemap, graphics, GPS, diagnostics, and widgets.

### Migrate Further To GeoBlazor/ArcGIS APIs

These are worth focused reduction work:

1. **ArcGIS widgets**
   - Problem: current widget classes trigger ArcGIS deprecation warnings.
   - Target: use GeoBlazor-supported ArcGIS web component equivalents if available, or disable widgets by default until a native component path is available.
   - Acceptance: no widget deprecation warnings during `/map?arcGisWidgets=true`.

2. **Marker click/hit-test handling**
   - Problem: selection depends on app parsing `Graphic.Attributes`.
   - Target: use the most direct GeoBlazor graphic click or hit-test API available.
   - Acceptance: pitch, encounter, and candidate selection still work without a broad custom adapter.

3. **Graphic synchronization**
   - Problem: `GameMap` owns repetitive clear/add/update code for every layer.
   - Target: extract a small app helper that syncs `GraphicsLayer` contents from typed data using GeoBlazor `Graphic`s.
   - Acceptance: `GameMap` loses repetitive layer plumbing but still owns game decisions.

4. **Popup/template policy**
   - Problem: map-native popups and RuckR overlays may duplicate detail surfaces.
   - Target: decide whether popups are kept for accessibility/quick summary or removed in favor of RuckR overlays.
   - Acceptance: one clear detail path for marker interaction.

5. **Lifecycle cancellation and diagnostics timers**
   - Problem: delayed diagnostics can fire after JS object disposal.
   - Target: cancellation-token based delayed diagnostics and expected disposal suppression.
   - Acceptance: navigation between debug routes produces no `ObjectDisposedException` telemetry noise.

### Keep In RuckR

These should not be migrated to GeoBlazor:

- GPS permission flow, status chip, accuracy messaging, and retry UX.
- Pitch capture eligibility and recruitment rules.
- Candidate pitch creation.
- API fetch cadence and server validation.
- Fluxor state.
- Game progress, onboarding, and product overlays.
- Telemetry ingestion and operational Jaeger access policy.

## Execution Plan

### Phase 1: Stabilize Diagnostics Noise

Scope:

- Add cancellation for delayed map diagnostic tasks.
- Treat disposed JS object references as expected when the component is disposing.
- Keep `settled-10s` as the default lightweight health sample.

Files:

- `RuckR/Client/Pages/GameMap.razor`
- Maybe `RuckR/Client/Pages/DebugMap.razor`

Verification:

- Navigate quickly between `/map`, `/debug-map`, and debug flag links.
- Confirm no `MapDiagnostics failed ... ObjectDisposedException` warnings.
- `dotnet build RuckR/Server/RuckR.Server.csproj --no-restore`.

### Phase 2: Research Widget Replacement

Status: complete locally. GeoBlazor Core 4.4.4 includes `HomeWidget`, `CompassWidget`, `LocateWidget`, and `ScaleBarWidget`, plus view models that reference the newer ArcGIS web components in documentation, but it does not expose direct Blazor components for `<arcgis-home>`, `<arcgis-locate>`, `<arcgis-scale-bar>`, or `<arcgis-compass>`. The current project keeps `Map:EnableArcGisWidgets=false` by default and treats `arcGisWidgets=true` as a compatibility/debug path only.

Scope:

- Check current GeoBlazor docs/source for ArcGIS web component widget support.
- Determine whether `HomeWidget`, `LocateWidget`, and `ScaleBarWidget` have component replacements.
- If supported, replace deprecated widgets.
- If not supported, keep widgets disabled by default and document the reason.

Files:

- `RuckR/Client/Pages/GameMap.razor`
- `RuckR/Client/Pages/DebugLayers.razor`
- `docs/plan/map-spec.md`

Verification:

- `/map?arcGisWidgets=true` renders.
- Browser console has no ArcGIS widget deprecation warnings, or the limitation is documented.

### Phase 3: Reduce Graphic Sync Plumbing

Status: complete locally. GeoBlazor `Graphic` creation now lives in `RuckR.Client.MapRendering.MapGraphicFactory`, and repeated layer replacement/grouping mechanics now live in `RuckR.Client.MapRendering.GraphicsLayerSync`. `GameMap` still owns game decisions, data fetch cadence, and which entities belong in which layers.

Scope:

- Extract marker symbol factories and graphic factories out of `GameMap`.
- Consolidate repeated layer clear/add/update routines.
- Preserve separate layers for pitch type if that remains useful for visibility/styling.

Candidate target files:

- `RuckR/Client/Pages/GameMap.razor`
- New `RuckR/Client/Map/MapGraphicFactory.cs`
- New `RuckR/Client/Map/GraphicsLayerSync.cs`

Verification:

- All three tested Pixel combinations still render:
  - `/map?basemap=styled&mapGraphics=true&autoGps=false`
  - `/map?basemap=styled&mapGraphics=false&autoGps=true`
  - `/map?basemap=styled&mapGraphics=true&autoGps=true`
- Pitch, encounter, candidate, and player markers still update.

### Phase 4: Reduce Selection Adapter

Scope:

- Research GeoBlazor click/hit-test support.
- Replace manual `AttributesDictionary` parsing if GeoBlazor exposes a better typed path.
- If not, isolate parsing in a tiny adapter and stop expanding it in `GameMap`.

Files:

- `RuckR/Client/Pages/GameMap.razor`
- Possible new `RuckR/Client/Map/MapSelectionAdapter.cs`

Verification:

- Tap pitch marker: pitch overlay opens.
- Tap encounter marker: encounter overlay opens.
- Tap candidate marker: candidate overlay opens.
- Empty-map tap does not clear or corrupt selection unexpectedly.

### Phase 5: Decide Popup Policy

Scope:

- Choose between map-native popups and RuckR overlays.
- If overlays are the canonical interaction, remove or minimize `PopupTemplate`s.
- If popups stay, keep their content brief and ensure they do not conflict with overlays on mobile.

Verification:

- Mobile tap behavior remains predictable on Pixel.
- No duplicate/conflicting detail UI.

## Definition Of Done

- The map remains healthy on Pixel for baseline, graphics-only, GPS-only, and full gameplay routes.
- `GameMap.razor` no longer grows as a framework wrapper around GeoBlazor.
- Custom code exists only for RuckR game behavior, product UI, data loading, and diagnostics.
- ArcGIS/GeoBlazor rendering behavior is handled through GeoBlazor or documented ArcGIS component APIs.
- Debug routes remain available during testing but can be hidden from nav with `Map:ShowDebugNav=false`.
