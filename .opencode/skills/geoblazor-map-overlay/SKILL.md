---
name: geoblazor-map-overlay
description: Add graphics/markers (overlays) to the GeoBlazor MapView in RuckR. USE FOR: placing pitch markers, encounter markers, user position dots, or any point overlay on the map. Covers programmatic graphic creation, symbol styling, hit-test click handling, and batch updates. DO NOT USE FOR: basemap configuration, widget setup, or non-overlay map concerns (use geoblazor skill).
license: MIT
compatibility: opencode
metadata:
  library: dymaptic.GeoBlazor.Core 4.4.4
  bug_workaround: InvalidChildException when GraphicsLayer is a child of MapView — use MapView.AddGraphic() instead
---

# GeoBlazor Map Overlay — Adding Graphics to the MapView

## Critical Bug in GeoBlazor Core 4.4.4

**NEVER use `<GraphicsLayer>` as a child of `<MapView>` in Razor markup.** It throws `InvalidChildException` at runtime. This is a confirmed bug in GeoBlazor Core 4.4.4.

**ALSO avoid `<GraphicsLayer>` as a child of `<Map>`** — it may work for static declarative graphics but fails when dynamically adding/removing graphics at runtime.

**Safe path:** Use the **programmatic C# API** (`MapView.AddGraphic()`) to add graphics directly to `view.graphics`. No `<GraphicsLayer>` component needed.

## Architecture Overview

```
MapView @ref="_mapView"
├── Map
│   └── Basemap (no GraphicsLayer here)
└── Widgets

Graphics are added/removed via C# code:
  _mapView.AddGraphic(graphic)
  _mapView.RemoveGraphic(graphic)
  _mapView.ClearGraphics()
```

## Required Usings

```csharp
@using dymaptic.GeoBlazor.Core.Components
@using dymaptic.GeoBlazor.Core.Components.Geometries
@using dymaptic.GeoBlazor.Core.Components.Symbols
@using dymaptic.GeoBlazor.Core.Enums
@using dymaptic.GeoBlazor.Core.Events
@using dymaptic.GeoBlazor.Core.Model
```

## Creating a Point Marker

### Step 1: Build the Symbol

```csharp
// Pitch marker — green circle with white border
var pitchSymbol = new SimpleMarkerSymbol(
    outline: new Outline(
        color: new MapColor("#FFFFFF"),
        width: new Dimension(2)
    ),
    color: new MapColor(47, 127, 79),  // R, G, B — green
    size: new Dimension(14),
    style: SimpleMarkerSymbolStyle.Circle
);
```

### Step 2: Create the Geometry

```csharp
var point = new Point(longitude: -0.1278, latitude: 51.5074);
```

### Step 3: Create Attributes (for hit-test identification)

```csharp
var attributes = new AttributesDictionary(new Dictionary<string, string>
{
    ["_ruckrType"] = "pitch",
    ["_ruckrId"] = pitch.Id.ToString(),
    ["name"] = pitch.Name,
    ["pitchType"] = pitch.Type.ToString()
});
```

### Step 4: Create and Add the Graphic

```csharp
var graphic = new Graphic(
    geometry: point,
    symbol: pitchSymbol,
    attributes: attributes
);

await _mapView.AddGraphic(graphic);
```

## Symbol Color Palette

Use these colors for consistent marker styling:

| Marker Type | MapColor | Hex |
|---|---|---|
| User position | `new MapColor(50, 120, 255)` | `#3278FF` |
| Standard pitch | `new MapColor(47, 127, 79)` | `#2F7F4F` |
| Training pitch | `new MapColor(62, 163, 109)` | `#3EA36D` |
| Stadium pitch | `new MapColor(196, 147, 18)` | `#C49312` |
| Common encounter | `new MapColor(107, 114, 128)` | `#6B7280` |
| Uncommon encounter | `new MapColor(47, 133, 90)` | `#2F855A` |
| Rare encounter | `new MapColor(37, 99, 235)` | `#2563EB` |
| Epic encounter | `new MapColor(217, 119, 6)` | `#D97706` |
| Legendary encounter | `new MapColor(200, 30, 30)` | `#C81E1E` |

## MapColor Constructor Reference

```csharp
// From RGB (0-255 each, alpha defaults to 1.0)
new MapColor(30, 144, 255)

// From RGBA (alpha is 0-1, NOT 0-255)
new MapColor(30, 144, 255, 0.5)  // semi-transparent

// From hex string
new MapColor("#1E90FF")

// From CSS color name
new MapColor("dodgerblue")
```

**Alpha gotcha:** Alpha is 0.0-1.0, not 0-255.

## Dimension Type

Symbol sizes and outline widths use `dymaptic.GeoBlazor.Core.Model.Dimension`:

```csharp
new Dimension(14)   // 14 points
new Dimension(2)    // 2 points (outline width)
```

## Batch Adding Graphics

```csharp
var graphics = pitches.Select(p => new Graphic(
    geometry: new Point(longitude: p.Longitude, latitude: p.Latitude),
    symbol: GetPitchSymbol(p.Type),
    attributes: new AttributesDictionary(new Dictionary<string, string>
    {
        ["_ruckrType"] = "pitch",
        ["_ruckrId"] = p.Id.ToString()
    })
)).ToList();

await _mapView.AddGraphics(graphics, CancellationToken.None);
```

## Removing Graphics

You **must keep a reference** to each `Graphic` object to remove it later:

```csharp
// Track by entity ID
private readonly Dictionary<int, Graphic> _pitchGraphics = new();

async Task UpdatePitchMarkersAsync(IReadOnlyList<PitchModel> pitches)
{
    // Remove old pitch graphics
    foreach (var graphic in _pitchGraphics.Values)
    {
        await _mapView.RemoveGraphic(graphic);
    }
    _pitchGraphics.Clear();

    // Add new ones
    foreach (var pitch in pitches)
    {
        var graphic = new Graphic(
            geometry: new Point(longitude: pitch.Longitude, latitude: pitch.Latitude),
            symbol: GetPitchSymbol(pitch.Type),
            attributes: new AttributesDictionary(new Dictionary<string, string>
            {
                ["_ruckrType"] = "pitch",
                ["_ruckrId"] = pitch.Id.ToString()
            })
        );
        await _mapView.AddGraphic(graphic);
        _pitchGraphics[pitch.Id] = graphic;
    }
}
```

Or clear all at once:

```csharp
await _mapView.ClearGraphics();
_pitchGraphics.Clear();
```

## Click Handling with HitTest

### MapView OnClick Setup

```razor
<MapView @ref="_mapView"
         OnClick="HandleMapClick"
         ... />
```

### Handler with HitTest

```csharp
private async Task HandleMapClick(ClickEvent clickEvent)
{
    if (clickEvent?.MapPoint is null || _mapView is null)
        return;

    try
    {
        var hitResult = await _mapView.HitTest(clickEvent);

        foreach (var hit in hitResult.Results)
        {
            if (hit is GraphicHit graphicHit)
            {
                var attrs = graphicHit.Graphic.Attributes;
                if (attrs.TryGetValue("_ruckrType", out var type) && type == "pitch")
                {
                    if (attrs.TryGetValue("_ruckrId", out var idStr)
                        && int.TryParse(idStr, out var pitchId))
                    {
                        // Dispatch to Fluxor or update UI state
                        Dispatcher.Dispatch(new SelectPitchAction(pitchId));
                        return;
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        Logger.LogWarning(ex, "Map click hit-test failed");
    }
}
```

### HitTest with Options (Filter by Type)

```csharp
// Only check pitch graphics, skip encounters and user marker
var hitResult = await _mapView.HitTest(clickEvent);
var pitchHit = hitResult.Results
    .OfType<GraphicHit>()
    .FirstOrDefault(h => h.Graphic.Attributes["_ruckrType"] == "pitch");
```

## User Position Marker Pattern

```csharp
private Graphic? _userGraphic;

async Task UpdateUserMarkerAsync(double latitude, double longitude)
{
    if (_userGraphic is not null)
    {
        await _mapView.RemoveGraphic(_userGraphic);
    }

    _userGraphic = new Graphic(
        geometry: new Point(longitude: longitude, latitude: latitude),
        symbol: new SimpleMarkerSymbol(
            outline: new Outline(
                color: new MapColor("#FFFFFF"),
                width: new Dimension(2)
            ),
            color: new MapColor(50, 120, 255),
            size: new Dimension(12),
            style: SimpleMarkerSymbolStyle.Circle
        ),
        attributes: new AttributesDictionary(new Dictionary<string, string>
        {
            ["_ruckrType"] = "user"
        })
    );

    await _mapView.AddGraphic(_userGraphic);
}
```

## Pitch Symbol Selector

```csharp
private static SimpleMarkerSymbol GetPitchSymbol(PitchType type) => type switch
{
    PitchType.Standard => new SimpleMarkerSymbol(
        outline: new Outline(color: new MapColor("#FFFFFF"), width: new Dimension(2)),
        color: new MapColor(47, 127, 79),
        size: new Dimension(14),
        style: SimpleMarkerSymbolStyle.Circle
    ),
    PitchType.Training => new SimpleMarkerSymbol(
        outline: new Outline(color: new MapColor("#FFFFFF"), width: new Dimension(2)),
        color: new MapColor(62, 163, 109),
        size: new Dimension(14),
        style: SimpleMarkerSymbolStyle.Circle
    ),
    PitchType.Stadium => new SimpleMarkerSymbol(
        outline: new Outline(color: new MapColor("#FFFFFF"), width: new Dimension(2)),
        color: new MapColor(196, 147, 18),
        size: new Dimension(16),
        style: SimpleMarkerSymbolStyle.Diamond
    ),
    _ => new SimpleMarkerSymbol(
        outline: new Outline(color: new MapColor("#FFFFFF"), width: new Dimension(2)),
        color: new MapColor(107, 114, 128),
        size: new Dimension(12),
        style: SimpleMarkerSymbolStyle.Circle
    )
};
```

## Encounter Symbol by Rarity

```csharp
private static SimpleMarkerSymbol GetEncounterSymbol(string rarity) => rarity switch
{
    "Common" => new SimpleMarkerSymbol(
        color: new MapColor(107, 114, 128), size: new Dimension(10),
        style: SimpleMarkerSymbolStyle.Circle),
    "Uncommon" => new SimpleMarkerSymbol(
        color: new MapColor(47, 133, 90), size: new Dimension(12),
        style: SimpleMarkerSymbolStyle.Circle),
    "Rare" => new SimpleMarkerSymbol(
        color: new MapColor(37, 99, 235), size: new Dimension(14),
        style: SimpleMarkerSymbolStyle.Diamond),
    "Epic" => new SimpleMarkerSymbol(
        color: new MapColor(217, 119, 6), size: new Dimension(16),
        style: SimpleMarkerSymbolStyle.Diamond),
    "Legendary" => new SimpleMarkerSymbol(
        color: new MapColor(200, 30, 30), size: new Dimension(18),
        style: SimpleMarkerSymbolStyle.Diamond),
    _ => new SimpleMarkerSymbol(
        color: new MapColor(200, 200, 200), size: new Dimension(10),
        style: SimpleMarkerSymbolStyle.Circle)
};
```

## Complete API Reference

| Method | Level | Description |
|--------|-------|-------------|
| `_mapView.AddGraphic(graphic)` | View | Add one graphic to `view.graphics` |
| `_mapView.AddGraphics(graphics, ct)` | View | Batch add graphics |
| `_mapView.RemoveGraphic(graphic)` | View | Remove one graphic (needs same reference) |
| `_mapView.ClearGraphics()` | View | Remove all view-level graphics |
| `_mapView.HitTest(clickEvent)` | View | Find graphics at click point |
| `_mapView.HitTest(screenPoint)` | View | Find graphics at screen coordinates |

## Lifecycle / Disposal

```csharp
@implements IAsyncDisposable

public async ValueTask DisposeAsync()
{
    if (_mapView is not null)
    {
        await _mapView.ClearGraphics();
    }
    _pitchGraphics.Clear();
    _encounterGraphics.Clear();
    _userGraphic = null;
}
```

## Common Pitfalls

1. **Never use `<GraphicsLayer>` in Razor** — triggers InvalidChildException in 4.4.4
2. **Alpha is 0-1**, not 0-255 in `MapColor` constructor
3. **`AttributesDictionary` values are strings** — serialize IDs with `.ToString()`
4. **Must keep `Graphic` references** for `RemoveGraphic()` — track in a dictionary
5. **`HitTest` returns empty** if graphics were added via JS interop (not through GeoBlazor API) — stick to one approach
6. **`Dimension` type required** for symbol sizes and outline widths
7. **`Outline` is a separate type** from `SimpleLineSymbol` — use `dymaptic.GeoBlazor.Core.Components.Symbols.Outline`

## Files This Skill Relates To

| File | Role |
|------|------|
| `RuckR/Client/Pages/Map.razor` | Main map page — render MapView, handle clicks, manage overlay lifecycle |
| `RuckR/Client/Store/MapFeature/MapState.cs` | Fluxor state — tracks visible pitches, encounters, selection |
| `RuckR/Client/Store/MapFeature/MapActions.cs` | Fluxor actions — SelectPitch, SetPitches, ClearSelection |
| `RuckR/Client/Services/ApiClientService.cs` | HTTP client — fetches nearby pitches and encounters |
| `RuckR/Client/wwwroot/js/arcgis-graphics.module.js` | **Legacy** JS interop graphics module — to be replaced by this approach |
| `docs/plan/map-spec.md` | Map spec — phases, marker colors, data flow |
