# RuckR Map Spec

## Purpose

The map is the player's primary game surface. It must render reliably, show the player's location, display nearby game objects (pitches, encounters), and enable the core game loop: discover, approach, capture, battle.

## Current State

A minimal GeoBlazor `MapView` renders at `/map` (and `/`) with:
- ArcGIS Navigation basemap
- Zoom, Home, Compass, Locate, and ScaleBar widgets
- Click handler (logs coordinates)
- Graceful fallback when API key is missing
- No GPS, no markers, no game logic

The full game map (backed up as `Map.full.razor.bak`) has all game features but was complex and coupled to a PortalItem/WebMap + custom JS interop graphics module.

## What The Map Must Accomplish

### P0 — Must Have (MVP)

1. **Render a map** — GeoBlazor `MapView` with ArcGIS basemap, centered on London by default. Must work in Blazor WASM with API key from env vars.

2. **Show player location** — Blue circle marker at the player's GPS position. Updates in real-time as the player moves. Uses browser Geolocation API via JS interop.

3. **Show nearby pitches** — Pitch markers within 500m browse radius. Each pitch is a colored marker on the map. Marker style indicates pitch type (Standard/Training/Stadium).

4. **Pitch click/tap selection** — Tapping a pitch marker selects it and opens a bottom sheet overlay showing:
   - Pitch name
   - Pitch type badge
   - Distance bucket (not exact meters)
   - Available player count
   - Capture action (enabled only when eligible)

5. **Show nearby encounters** — Wild rugby player encounters within 5km. Colored by rarity (Common=gray, Uncommon=green, Rare=blue, Epic=purple, Legendary=gold). Tapping opens encounter overlay with recruit action.

6. **Proximity-based capture** — Capture Players button is enabled only when:
   - Player GPS accuracy <= 50m
   - Player within 100m of the pitch
   - Server validates the capture server-side

7. **Distance privacy** — Other players never see exact GPS. Display only buckets: `<50m`, `<100m`, `<250m`, `<500m`, `>500m`.

8. **Graceful degradation** — Map works without GPS (shows default center). Map works without API key (shows fallback panel). API failures show retry banner, don't crash.

### P1 — Should Have

9. **Locate-me button** — Centers and zooms to player position. Already provided by `LocateWidget`.

10. **Proximity-aware marker styling** — Pitches within 100m get a glow/pulse animation to indicate they are "discoverable."

11. **Center-on-closest shortcuts** — Buttons to center on nearest Standard, Training, or Stadium pitch.

12. **Game progress chip** — Shows level and XP progress bar in the top-right corner.

13. **Onboarding banner** — First-time visitors see a dismissible banner explaining the map. Stored in localStorage.

### P2 — Nice To Have

14. **Real-time marker updates via SignalR** — Other players' actions (new pitch creation) appear without polling. Reconnection recovers state.

15. **Create pitch from map** — "No nearby pitches?" prompt with link to pitch creation flow.

16. **Custom rugby-ball markers** — Replace circle markers with rugby-ball shaped CSS `divIcon` markers for thematic consistency.

17. **Map remembers position** — Last known center/zoom persisted in localStorage for faster return visits.

## Technical Architecture

### Component Stack

```
Map.razor
├── MapView (GeoBlazor)
│   ├── Map > Basemap > BasemapStyle (ArcGIS Navigation)
│   ├── GraphicsLayer (pitch markers + encounter markers + user marker)
│   ├── HomeWidget
│   ├── CompassWidget
│   ├── LocateWidget
│   └── ScaleBarWidget
├── Pitch selection overlay (bottom sheet)
├── Encounter selection overlay (bottom sheet)
├── Game progress chip
├── Center-on-closest buttons
└── Status banners (GPS disabled, onboarding, loading, error)
```

### State Management

- **Fluxor** `MapState`: visible pitches, visible encounters, selected pitch ID, selected encounter ID, map readiness
- **Fluxor** `LocationState`: user lat/lng, accuracy, GPS status, error message
- **Page state** (local to Map.razor): loading flag, error flag, GPS disabled flag, onboarding flag, capture eligibility

### Data Flow

1. Player opens `/map`
2. Map renders with default center (London)
3. Browser GPS watch starts (non-blocking)
4. On first position: map centers on player, user marker appears
5. Client fetches nearby pitches (`GET /api/pitches/nearby?lat=&lng=&radius=500`)
6. Client fetches nearby encounters (`GET /api/encounters?lat=&lng=&radius=5000`)
7. Markers render on map
8. Player taps a pitch marker → pitch overlay opens → capture eligibility checked
9. Player taps an encounter marker → encounter overlay opens → recruit action available
10. As player moves (>50m or >30s): refetch pitches/encounters, update markers

### Key Dependencies

| Dependency | Purpose | Status |
|---|---|---|
| `dymaptic.GeoBlazor.Core` 4.4.4 | Blazor ArcGIS component wrapper | Installed |
| ArcGIS API Key | Map tiles + basemap services | Via env var `ARC_GIS_API_KEY` |
| `Fluxor` | Client-side state management | Installed |
| Browser Geolocation API | Player GPS position | Via JS interop (`IGeolocationService`) |
| `SignalR` | Real-time updates (optional P2) | Installed |
| `ApiClientService` | HTTP calls to server APIs | Installed |

### Marker Colors

| Object | Color | Hex |
|---|---|---|
| User position | Blue | `#3278FF` |
| Standard pitch | Green | `#2F7F4F` |
| Training pitch | Light green | `#3EA36D` |
| Stadium pitch | Gold | `#C49312` |
| Common encounter | Gray | `#6B7280` |
| Uncommon encounter | Green | `#2F855A` |
| Rare encounter | Blue | `#2563EB` |
| Epic encounter | Amber | `#D97706` |
| Legendary encounter | Red | `#C81E1E` |

## Implementation Phases

### Phase 1: Basemap + GPS Marker (current)
- GeoBlazor MapView with ArcGIS Navigation basemap
- GPS watch → user position marker on map
- Click handler for coordinate display

### Phase 2: Pitch Markers + Selection
- Fetch nearby pitches from API
- Render pitch markers on GraphicsLayer
- Pitch click → select → bottom sheet overlay
- Capture eligibility check

### Phase 3: Encounter Markers + Recruitment
- Fetch nearby encounters from API
- Render encounter markers with rarity colors
- Encounter click → recruit overlay
- Recruitment flow

### Phase 4: Polish + Real-Time
- Proximity-aware marker styling (pulse for discoverable)
- SignalR for live updates
- Game progress chip
- Onboarding banner
- Center-on-closest buttons
- localStorage position persistence

## Success Criteria

- [ ] Map renders within 3 seconds on a modern phone browser
- [ ] Player marker appears within 5 seconds of GPS permission
- [ ] Pitch markers render within 2 seconds of position fix
- [ ] Tapping a pitch opens the overlay within 500ms
- [ ] Capture button is never enabled when the player is out of range
- [ ] Map remains usable when GPS is denied or API calls fail
- [ ] No memory leaks after 30 minutes of continuous map usage
