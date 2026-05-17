# Map UI Smoke Checklist

Use this after changes to `RuckR/Client/Pages/GameMap.razor`, `RuckR/Client/Pages/DebugMap.razor`, GeoBlazor widgets/layers, map overlays, GPS prompts, or production deploys that affect the map.

## Desktop

1. Open `https://ruckr.exe.xyz/map`.
2. Wait for the ArcGIS controls to become enabled.
3. Verify the top-left ArcGIS control stack is usable: zoom in, zoom out, Home, Compass, and Locate when browser permission allows it.
4. Verify the pitch legend does not overlap the ArcGIS controls.
5. Verify the recruit board does not overlap the pitch legend.
6. Pan or use a nearest-pitch button, then click Home and verify the map returns to the initial home position.
7. With GPS unavailable, click `Retry GPS` five times and verify the prompt hides until a full page reload.
8. With GPS available but inaccurate, verify the GPS prompt does not appear; low accuracy should surface only in recruit/capture eligibility messaging.

## Mobile Width

1. Repeat the desktop checks at roughly `390x844`.
2. Verify the legend remains below the top-left ArcGIS controls.
3. Verify the recruit board stays anchored near the bottom and does not cover the legend.
4. Verify toast and encounter overlays do not cover primary map controls.

## Quick DOM Checks

When using browser automation, compare bounding boxes for:

- `.pitch-legend`
- `.encounter-radar`
- `.esri-ui-top-left`
- `.esri-locate`

The legend must not intersect the ArcGIS top-left controls.
