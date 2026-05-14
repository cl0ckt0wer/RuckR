---
name: geoblazor
description: dymaptic GeoBlazor — Blazor wrapper for the ArcGIS Maps SDK. USE FOR: writing Blazor map components, adding FeatureLayer/GraphicsLayer, using widgets (Home, Compass, LayerList, Legend, Editor, Search, BasemapToggle), geometry operations, hit tests/queries, authentication (API key or OAuth), serving ArcGIS asset files, JS extensions, and managing MapView/SceneView. DO NOT USE FOR: Leaflet, Mapbox, or non-ArcGIS map libraries (use other skills). Docs: https://docs.geoblazor.com/
license: MIT
compatibility: opencode
metadata:
  library: dymaptic.GeoBlazor.Core
  docs: https://docs.geoblazor.com/
  samples: https://samples.geoblazor.com
  nuget: dymaptic.GeoBlazor.Core
  framework: Blazor
---

# GeoBlazor Skill Guide

## Overview

GeoBlazor is a Blazor component library that wraps the ArcGIS Maps SDK for JavaScript. It lets you build interactive maps in Blazor Server and Blazor WebAssembly without writing JavaScript.

Two editions:
- **Core** (free, MIT) — open-source on NuGet as `dymaptic.GeoBlazor.Core`
- **Pro** (paid) — additional widgets, layers, and features as `dymaptic.GeoBlazor.Pro`

## Setup

### NuGet
```xml
<PackageReference Include="dymaptic.GeoBlazor.Core" Version="*" />
<!-- or for Pro: -->
<!-- <PackageReference Include="dymaptic.GeoBlazor.Pro" Version="*" /> -->
```

### Program.cs (Server)
```csharp
builder.Services.AddGeoBlazor();
```

### Program.cs (WASM)
```csharp
builder.Services.AddGeoBlazor(builder.HostEnvironment);
```

### Serving Asset Files
GeoBlazor needs ArcGIS SDK assets served from `_content/dymaptic.GeoBlazor.Core/assets/`. In Server mode this is automatic. In WASM you may need to configure static assets or use the Pro asset proxy.

## Core Components

### MapView (2D) / SceneView (3D)
```razor
<MapView @ref="mapView" Style="height: 600px; width: 100%;">
    <Map ArcGisApiKey="@apiKey">
        <FeatureLayer Url="https://services.example.com/arcgis/..." />
    </Map>
</MapView>
```

Wrap in an `<EditForm>` or render as a standalone component. Use `@ref` to access the view from code-behind.

### Map
- **Basemap**: `Basemap` component with `BasemapStyle` (e.g., `BasemapStyleName.Oceans`, `Streets`, `Satellite`)
- **Ground** (3D): Optional `Ground` component for elevation
- **Initial extent**: Set via `MapView.SceneView` properties or `ViewExtent` parameter

### Layers
| Component | Description |
|-----------|-------------|
| `FeatureLayer` | ArcGIS feature service layer |
| `MapImageLayer` | Dynamic map service layer |
| `TileLayer` | Pre-rendered tile service |
| `GraphicsLayer` | Client-side graphics (no URL) |
| `GeoJSONLayer` | GeoJSON file/data layer |
| `CSVLayer` | CSV data layer |
| `BingMapsLayer` | Bing Maps imagery |
| `ElevationLayer` | 3D elevation data |
| `GeoRSSLayer` | GeoRSS feed layer |
| `GroupLayer` | Groups multiple layers |
| `ImageryLayer` | Image service layer |
| `KMLLayer` | KML file layer |
| `OpenStreetMapLayer` | OpenStreetMap tiles |
| `WFSLayer` | WFS feature layer |
| `WMSLayer` | WMS raster layer |
| `WMTSLayer` | WMTS tile layer |
| `WebTileLayer` | Custom tile URL layer |

### GraphicsLayer & Graphics
```razor
<GraphicsLayer>
    <Graphic>
        <Point Longitude="-118.805" Latitude="34.027" />
        <SimpleMarkerSymbol Style="SimpleMarkerSymbolStyle.Circle"
                            Color="@("#FF0000")" Size="12" />
    </Graphic>
</GraphicsLayer>
```

### Symbols
- `SimpleMarkerSymbol` — Point symbols (circle, square, cross, diamond, triangle, x)
- `SimpleLineSymbol` — Line symbols (solid, dash, dot, dash-dot)
- `SimpleFillSymbol` — Polygon fill symbols
- `PictureMarkerSymbol` — Custom image for points
- `TextSymbol` — Label text
- `WebStyleSymbol` — ArcGIS web style symbols

### Geometry Types
- `Point` (Longitude, Latitude)
- `Multipoint` (collection of points)
- `Polyline` (paths)
- `Polygon` (rings)
- `Extent` (bounding box)
- `Circle` (center + radius)

## Widgets

Widgets are child components of `MapView`:
```razor
<MapView>
    <HomeWidget />
    <CompassWidget />
    <ZoomWidget />
    <BasemapToggleWidget>
        <BasemapStyle Name="@BasemapStyleName.Satellite" />
    </BasemapToggleWidget>
    <BasemapGalleryWidget />
    <LayerListWidget />
    <LegendWidget />
    <EditorWidget />
    <SearchWidget />
    <ExpandWidget>
        <LayerListWidget />
    </ExpandWidget>
    <MeasurementWidget />
    <BookmarksWidget />
</MapView>
```

Pro has additional widgets not in Core (e.g., directional measurement, printing).

## Authentication

### API Key (simplest)
```razor
<Map ArcGisApiKey="your-api-key">
```

### OAuth (ArcGIS Identity)
Configure in `appsettings.json` or via `GeoBlazorSettings`:
```json
{
  "GeoBlazor": {
    "AppId": "your-app-id",
    "PortalUrl": "https://www.arcgis.com"
  }
}
```

## Events

GeoBlazor surfaces ArcGIS events as Blazor event callbacks:
- `OnClick`, `OnDoubleClick`, `OnPointerDown`, `OnPointerUp`, `OnPointerMove`
- `OnDragStart`, `OnDrag`, `OnDragEnd`
- `OnKeyDown`, `OnKeyUp`
- `OnLayerViewCreate`
- `OnViewChange`, `OnViewExtentChange`

## Hit Tests & Queries

### Hit Test (click to identify features)
```csharp
HitTestResult result = await mapView.HitTest(screenPoint);
foreach (GraphicHit hit in result.Results)
{
    // inspect hit.Graphic.Attributes
}
```

### FeatureLayer Query
```csharp
FeatureSet featureSet = await featureLayer.QueryFeatures(new Query
{
    Where = "POPULATION > 100000",
    OutFields = new[] { "NAME", "POPULATION" }
});
```

## Geometry Engine

```csharp
GeometryEngine engine = new();
double distance = engine.Distance(point1, point2, GeometryEngineLinearUnit.Miles);
Polygon buffer = engine.Buffer(geometry, 100, GeometryEngineLinearUnit.Meters);
bool intersects = engine.Intersects(geometry1, geometry2);
```

Available operations: `Buffer`, `Clip`, `Contains`, `Crosses`, `Cut`, `Difference`, `Disjoint`, `Distance`, `Equals`, `Extent`, `Generalize`, `Intersect`, `Intersects`, `IsSimple`, `Length`, `NearestCoordinate`, `NearestPoint`, `NormalizeCentralMeridian`, `Offset`, `Overlaps`, `Relate`, `Rotate`, `Scale`, `Simplify`, `SymmetricDifference`, `Touches`, `TrimExtend`, `Union`, `Unsmooth`, `Within`.

## JavaScript Extensions

For custom ArcGIS JS SDK functionality not wrapped by GeoBlazor:
```csharp
await mapView.JsModule.InvokeVoidAsync("functionName", args);
```

Use `IJSObjectReference` for custom interop with the ArcGIS JS objects.

## Working with Extent

```csharp
// Set initial extent
<MapView>
    <Map>
        <Extent XMin="-122.5" YMin="37.5" XMax="-122.0" YMax="37.9"
                SpatialReference="wkid:4326" />
    </Map>
</MapView>

// Programmatic zoom/pan
await mapView.ZoomTo(geometry);
await mapView.CenterAt(point);
```

## Popups

```razor
<FeatureLayer Url="...">
    <PopupTemplate>
        <Title>"Feature Info"</Title>
        <Content>
            <FieldsPopupContent>
                <FieldInfo FieldName="NAME" Label="Name" />
                <FieldInfo FieldName="POPULATION" Label="Population" />
            </FieldsPopupContent>
        </Content>
    </PopupTemplate>
</FeatureLayer>
```

Popup content types: `FieldsPopupContent`, `TextPopupContent`, `MediaPopupContent`, `AttachmentsPopupContent`, `ExpressionPopupContent`.

## Renderers

Control how features are drawn:
- `SimpleRenderer` — uniform symbol for all features
- `UniqueValueRenderer` — different symbols by attribute value
- `ClassBreaksRenderer` — graduated symbols by numeric range
- `HeatmapRenderer` — density heatmap
- `DotDensityRenderer` — dot density maps (Pro)
- `BlendRenderer` — blended imagery layers (Pro)

```razor
<FeatureLayer Url="...">
    <SimpleRenderer>
        <SimpleFillSymbol Color="@("#00FF0088")" 
                          Outline="new SimpleLineSymbol { Color = "#000000", Width = 1 }" />
    </SimpleRenderer>
</FeatureLayer>
```

## Pro-Specific Features

Only available in `dymaptic.GeoBlazor.Pro`:
- Directional measurement widgets
- Advanced printing/export
- 3D analysis tools
- Additional layer types
- Asset proxy for WASM hosting
- Priority support
