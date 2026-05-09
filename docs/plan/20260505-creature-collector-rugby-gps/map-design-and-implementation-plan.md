# Map Design And Implementation Plan

## Purpose

The map is the primary game surface for RuckR. It should make the player feel like they are exploring a live rugby world around them, while keeping the MVP technically simple: Leaflet renders the map, browser geolocation drives the user's position, the server owns proximity validation, and Fluxor keeps the UI reactive.

This document refines the existing PRD map requirements into a product design and implementation sequence.

## Current Baseline

The codebase already has the core map foundation:

- `RuckR/Client/Pages/Map.razor` renders `/map` and `/`, starts GPS watching, loads nearby pitches, reacts to pitch selection, and shows a bottom pitch overlay.
- `RuckR/Client/Services/MapService.cs` wraps Leaflet through `IJSObjectReference` and dispatches marker click actions back into Fluxor.
- `RuckR/Client/wwwroot/js/leaflet-map.module.js` initializes Leaflet, renders pitch markers, renders the user marker, centers the map, and disposes map resources.
- `RuckR/Client/Store/MapFeature/*` stores map readiness, visible pitches, and selected pitch id.
- `RuckR/Client/Store/LocationFeature/*` stores location state.
- `ApiClientService` already exposes nearby pitch and nearby player endpoints.

The remaining work is mostly experience quality, proximity rules, visual language, and reliability.

## Product Design

### Player Mental Model

The player should understand the map in four layers:

- You are the blue live dot.
- Rugby pitches are places to go.
- Nearby pitches unlock actions.
- The bottom sheet is the command center for the selected pitch.

The map should avoid exact competitive GPS exposure. It can use exact coordinates locally for rendering and server validation, but user-facing copy and player-to-player surfaces should use fuzzy buckets.

### Map Layout

Mobile is the primary layout.

- The map fills the available viewport under the main navigation.
- The user location control sits above the bottom safe area on the right.
- GPS, loading, and error banners appear at the top, above map controls.
- Selecting a pitch opens a bottom sheet that covers roughly 35-45% of mobile height and can be dismissed.
- Desktop keeps the full map as the main canvas and uses a right-side or bottom panel depending on available width. MVP can keep the bottom sheet for both mobile and desktop.

### Visual Language

Use a rugby training-ground aesthetic without adding heavy art assets:

- User marker: blue circle with white stroke and pulse.
- Standard pitch marker: green rugby-ball style marker.
- Training pitch marker: lighter green marker.
- Stadium marker: gold marker.
- Discoverable pitch marker: elevated or glowing marker when within 100m.
- Nearby but not discoverable marker: normal marker when within 500m.
- Out-of-range marker: optional muted marker only if broader map browsing is added later.

Avoid relying only on emoji markers long term. Emoji is acceptable for prototype, but the implementation should move toward CSS `divIcon` markers with accessible labels and stable cross-platform rendering.

### Map States

The page needs explicit UI states:

- First load: map shell and spinner while Leaflet initializes.
- GPS prompt: educational prompt that explains why location is needed.
- GPS denied: map centered on default London seed location, with a retry/enable GPS action.
- GPS inaccurate: warning that capture requires better accuracy, while browsing still works.
- Offline/API failure: map remains usable if already loaded, with a retry banner for pitch data.
- No nearby pitches: empty state bottom prompt with a link to create a pitch.
- Pitch selected: bottom sheet with pitch details, distance bucket, available player count, and actions.
- Discoverable pitch: capture action is enabled only when server-side proximity allows it.

### Interaction Flow

1. Player opens `/map`.
2. Leaflet initializes against the default center or last known location.
3. App requests GPS watch.
4. On first position, map centers on the user once.
5. User marker updates as GPS changes.
6. Client sends location to SignalR for ephemeral server-side validation and notifications.
7. Client fetches nearby pitches within 500m.
8. Pitches render as markers.
9. Player taps a pitch marker.
10. JS calls `MapService.OnPitchClicked`, which dispatches `SelectPitchAction`.
11. `Map.razor` opens the pitch sheet and loads nearby player count.
12. If within capture threshold, player can navigate into capture flow.

### Distance And Proximity Rules

Use two separate concepts:

- Browse radius: 500m. Pitches inside this radius show on the map and in the pitch sheet.
- Capture radius: 100m. Capture is only enabled when server validation confirms current location, timestamp, and accuracy.

Display distance as buckets only:

- `< 50m`
- `< 100m`
- `< 250m`
- `< 500m`
- `> 500m`

Do not show exact meters to other players. Exact client-side distance can be used only to choose the local bucket and improve UX.

### Pitch Bottom Sheet

The selected pitch sheet should contain:

- Pitch name.
- Pitch type badge.
- Distance bucket.
- Availability state: undiscovered, discoverable, captured, no players, or GPS accuracy needed.
- Available player count.
- Primary action: `Capture Players` when eligible, otherwise contextual disabled copy.
- Secondary action: `Challenge Nearby Player` only when there is a valid battle flow.
- Optional action: `Create Pitch Nearby` or `View Details` after pitch creation is fully supported.

The sheet should never imply capture is available unless the server will accept it. If eligibility is unknown, show `Checking range...` rather than enabling the button optimistically.

### Real-Time Behavior

SignalR should enhance the map, not be required for initial rendering.

- On connect or reconnect, immediately send the latest known location.
- On reconnect, refetch nearby pitches and pending battle notifications.
- Pitch discovery notifications should dispatch a Fluxor action and show a toast.
- Battle challenge notifications should stay separate from map marker rendering.
- If SignalR is disconnected, show a small connection indicator but allow map browsing and HTTP fetches.

### Privacy And Safety

- Do not expose exact user GPS to other clients.
- Store only ephemeral live location in `LocationTracker` unless a persisted audit point is required for collection records.
- Server capture validation should require a recent location update, acceptable accuracy, and proximity to the selected pitch.
- Client-side checks are UX only and must never be trusted for capture or battle validation.

## Technical Design

### Components

`Map.razor`

- Owns page-level UI state: loading, GPS disabled, selected pitch panel, player count.
- Subscribes to Fluxor map/location state and geolocation events.
- Calls `ApiClientService` for nearby pitches and player counts.
- Calls `SignalRClientService` for location updates.

`MapService`

- Owns JS interop lifecycle.
- Imports `./js/leaflet-map.module.js` in `OnAfterRenderAsync` flow.
- Converts `PitchModel` into marker DTOs.
- Holds `DotNetObjectReference` for marker click callbacks.
- Dispatches `SelectPitchAction` on marker click.

`leaflet-map.module.js`

- Owns Leaflet objects and DOM-level marker behavior.
- Should eventually support marker style updates without clearing all markers every time.
- Should keep public functions narrow and stable.

Fluxor map feature

- `MapState` should eventually track ready/error state, visible pitches, selected pitch id, and optionally pitch eligibility by id.
- Reducers remain pure.
- Effects should handle API calls only if the page starts to grow too much; MVP can keep local page calls.

Server APIs

- `GET api/pitches/nearby?lat=&lng=&radius=500` returns pitch DTOs safe for display.
- `GET api/players/nearby?lat=&lng=&radius=100` supports pitch sheet player count or capture list.
- `POST api/collection/capture` validates the authenticated user, pitch, player, recent tracked location, accuracy, and proximity.

### JS Interop Contract

Keep the map module contract stable:

```text
initMap(containerId, centerLat, centerLng, zoom) -> boolean
addPitchMarkers(markersJson, dotNetRef) -> void
updatePitchMarkerStyles(markerStylesJson) -> void
addUserMarker(lat, lng, accuracyMeters?) -> void
centerOn(lat, lng) -> void
clearPitchMarkers() -> void
dispose() -> void
```

Add `updatePitchMarkerStyles` only when the UI needs proximity-specific marker styling. Until then, `addPitchMarkers` can include style metadata in each marker DTO.

### Marker DTO Shape

Use a JS-friendly DTO instead of serializing full EF/shared models:

```json
{
  "id": 123,
  "latitude": 51.5074,
  "longitude": -0.1278,
  "name": "RuckR Training Ground",
  "type": "Training",
  "distanceBucket": "Within100m",
  "isDiscoverable": true,
  "isCollected": false
}
```

This avoids leaking model internals and makes marker styling deterministic.

## Implementation Plan

### Phase 1: Stabilize Current Map MVP

- Move inline map CSS from `Map.razor` into `Map.razor.css` or `app.css` so the page is easier to maintain.
- Replace emoji-only pitch marker HTML with CSS-backed `divIcon` markers for standard, training, stadium, and discoverable states.
- Add an explicit `Locate me` button that calls `CenterOnAsync` with the latest known location.
- Keep map initialized when GPS fails; show default center and a non-blocking GPS banner.
- Ensure `MapService.DisposeAsync` nulls references after disposal and tolerates repeated initialization.

Verification:

- `/map` renders with GPS allowed.
- `/map` renders with GPS denied.
- Marker click opens the pitch sheet.
- Navigating away and back does not duplicate map instances or marker callbacks.

### Phase 2: Add Proximity-Aware UX

- Extend marker DTOs with distance bucket and discoverable state.
- In `Map.razor`, compute local display buckets from current location and visible pitches.
- Disable `Capture Players` unless the selected pitch is locally within the capture bucket and the latest GPS accuracy is acceptable.
- Add `Checking range...`, `Move closer`, and `Improve GPS accuracy` states to the pitch sheet.
- Keep final capture validation server-side in `POST api/collection/capture`.

Verification:

- Pitch at `< 100m` shows discoverable styling and enabled capture flow.
- Pitch at `< 500m` shows nearby styling and disabled capture copy.
- Missing or inaccurate GPS disables capture without blocking browsing.

### Phase 3: Improve Data Refresh And Real-Time Recovery

- Throttle location-driven nearby pitch fetches so movement does not call the API on every GPS event.
- Refetch nearby pitches when the user moves more than a meaningful threshold, such as 50m, or after a time window, such as 30 seconds.
- On SignalR reconnect, send latest location and refetch nearby pitches.
- Surface connection status with the existing `ConnectionStatus` component.
- Dispatch pitch discovery notifications through Fluxor and the existing toast component.

Verification:

- Walking or simulated GPS updates do not spam `api/pitches/nearby`.
- SignalR reconnect restores location tracking and notifications.
- API failure leaves existing markers visible and shows a retry banner.

### Phase 4: Polish Pitch Sheet And Capture Flow

- Add pitch detail states: available players, already collected players, empty pitch, and capture success.
- Navigate capture with route/query context, for example `/players/nearby?pitchId=123`, instead of a generic `/players/nearby` route if that page exists.
- After a successful capture, update inventory state and selected pitch state without requiring full page refresh.
- Add a create-pitch prompt when there are no nearby pitches.

Verification:

- Capture from map lands on the correct pitch context.
- Successful capture updates collection and map UI.
- Empty map has a clear next action.

### Phase 5: Test Coverage And Regression Guardrails

- Add unit tests for distance bucket calculations if not already covered.
- Add service-level tests for capture validation rules.
- Add Playwright coverage for map loading, GPS denied state, marker selection, and pitch sheet actions using mocked geolocation.
- Add a disposal/regression test path if Playwright can navigate away and back to `/map`.

Verification:

- `dotnet build RuckR.sln` passes.
- `dotnet test` passes.
- Playwright map smoke test passes in headless browser with mocked geolocation.

## Acceptance Criteria

- Map loads as the default route and remains usable without GPS permission.
- User location renders as a distinct marker when GPS is available.
- Nearby pitch markers render within the 500m browse radius.
- Selected pitch opens a bottom sheet with name, type, distance bucket, player count, and contextual actions.
- Capture action is only enabled when the pitch is inside the capture threshold and GPS quality is acceptable.
- Server remains the source of truth for capture validation.
- SignalR reconnect recovers live location and notifications without requiring a page refresh.
- Map resources are disposed cleanly when navigating away from the page.

## Risks And Mitigations

- GPS denied or inaccurate: keep the map browsable and explain what is unavailable.
- Tile provider slowness: show the map shell and keep controls responsive; consider cached/local tile alternatives only after MVP.
- Excessive API calls from GPS watch: throttle by distance and time.
- JS memory leaks: keep all Leaflet objects inside the module and dispose on page teardown.
- Marker click callback leaks: dispose old `DotNetObjectReference` before replacing markers.
- Capture spoofing: validate on the server using recent SignalR location, accuracy, and SQL geography distance.

## Open Decisions

- Whether capture should happen directly inside the pitch sheet or navigate to a dedicated capture page.
- Whether manually created pitches should appear immediately for all nearby players via SignalR or only after polling/refetch.
- Whether the map should show all pitches in the viewport later, or only nearby pitches for MVP.
- Whether to persist last known map center in local storage for faster returning sessions.
