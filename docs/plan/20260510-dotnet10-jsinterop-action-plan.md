# .NET 10 JS Interop — RuckR Action Plan

**Date:** 2026-05-10
**Context:** Research into .NET 10 JS interop new features and how they apply to the RuckR codebase.

---

## 1. Add Asset Fingerprinting & ImportMap

**Priority:** High
**Effort:** Small

### Steps:
1. Add `<OverrideHtmlAssetPlaceholders>true</OverrideHtmlAssetPlaceholders>` to `RuckR.Client.csproj`
2. Add `<ImportMap />` component to `App.razor` `<head>` section
3. Verify build/publish works with fingerprinted assets

**Rationale:** .NET 10's built-in fingerprinting replaces the removed `BlazorCacheBootResources`. ImportMap auto-resolves fingerprinted JS modules. Zero runtime cost, significant caching benefit.

---

## 2. Migrate GeolocationService to Native JS Interop

**Priority:** High
**Effort:** Medium

### Current state:
- Uses `Blazor.Geolocation.WebAssembly` NuGet package (v9.0.1)
- Package is from a third-party, not maintained by Microsoft

### Target state:
- Replace with native JS interop using .NET 10's `InvokeConstructorAsync`
- Create `geolocation.module.js` in `wwwroot/js/`
- Wrap in a new `GeolocationInteropService` or refactor `GeolocationService`

### Steps:
1. Create `wwwroot/js/geolocation.module.js` exposing:
   - `createWatcher(options)` → returns object with `watchId`
   - `getCurrentPosition()` → Promise-based
   - `clearWatch(watchId)`
   - `dispose()` cleanup
2. Refactor `GeolocationService.cs`:
   - Replace `Blazor.Geolocation.WebAssembly.IGeolocationService` with `IJSRuntime`
   - Use `JS.InvokeConstructorAsync("GeolocationModule")` or `JS.InvokeAsync<IJSObjectReference>("import", "./js/geolocation.module.js")`
   - Keep existing EMA smoothing and null-island filtering logic
3. Remove `Blazor.Geolocation.WebAssembly` NuGet reference
4. Update tests (if any test against the geolocation interface)

**Rationale:** Eliminates third-party dependency, uses .NET 10's improved JS object lifecycle management, aligns with ADR-004/ADR-006 patterns.

---

## 3. Collocate JS Modules with Consuming Components

**Priority:** Medium
**Effort:** Small (rename + minor refactors)

### Current state:
- `client-logging.module.js` lives in `wwwroot/js/`
- No `.razor.js` collocated files exist

### Target state:
- Rename `client-logging.module.js` → `BrowserErrorLogger.razor.js` (next to `BrowserErrorLogger.cs`)
- Future JS modules follow the same collocation pattern

### Steps:
1. Rename `wwwroot/js/client-logging.module.js` → `BrowserErrorLogger.razor.js`
2. Update import path in `BrowserErrorLogger.cs` from `./js/client-logging.module.js` to `./BrowserErrorLogger.razor.js`
3. Update any build/publish scripts that reference the old path

**Rationale:** .NET 10 docs recommend collocating JS with consuming component for discoverability. Visual Studio 2022 shows `.razor.js` files nested under `.razor` in Solution Explorer.

---

## 4. Add DynamicDependency for JS-Invokable Classes

**Priority:** Medium
**Effort:** Small

### Steps:
1. Audit all classes with `[JSInvokable]` that are **not** Razor components
2. Add `[DynamicDependency(nameof(MethodName))]` to their constructors

### Current candidates:
- `BrowserErrorLogger` — has `[JSInvokable] LogBrowserError` → add `[DynamicDependency(nameof(LogBrowserError))]`
- Any future JS-invocable services

**Rationale:** Required for AOT/trimming survival in WASM apps. Not urgent now but needed before enabling AOT compilation.

---

## 5. Evaluate Wasm-Specific Settings

**Priority:** Low
**Effort:** Small

### Steps:
1. Set `<WasmEnableHotReload>true</WasmEnableHotReload>` in `RuckR.Client.csproj` (already default in Debug, but make explicit)
2. Test HttpClient streaming behavior — if any endpoint returns large payloads synchronously, add `<WasmEnableStreamingResponse>false</WasmEnableStreamingResponse>`
3. Consider `<WasmBundlerFriendlyBootConfig>true</WasmBundlerFriendlyBootConfig>` if adopting a JS bundler in future

---

## 6. Update Service Worker Registration

**Priority:** Low
**Effort:** Trivial

### Steps:
1. Update `service-worker.published.js` to include `updateViaCache: 'none'` in `register()` call:
```js
navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
```

**Rationale:** .NET 10 project template default. Prevents caching issues during SW updates.

---

## Timeline

| # | Action | Priority | Effort | Owner |
|---|--------|----------|--------|-------|
| 1 | OverrideHtmlAssetPlaceholders + ImportMap | High | Small | Agent |
| 2 | GeolocationService → native JS interop | High | Medium | Agent |
| 3 | Collocate JS modules (.razor.js) | Medium | Small | Agent |
| 4 | DynamicDependency on JS-invocable classes | Medium | Small | Agent |
| 5 | Wasm-specific settings evaluation | Low | Small | Agent |
| 6 | Service worker updateViaCache | Low | Trivial | Agent |

---

## Dependencies
- None external. All changes are within the RuckR codebase.
- Action #2 (Geolocation migration) should be done before enabling AOT compilation.
- Actions #1 and #3 can be done in parallel.