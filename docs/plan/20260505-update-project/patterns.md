# Patterns — plan 20260505-update-project

> Saved: 2026-05-05T22:26:00Z | Source: task-implementer (RuckR .NET 10 upgrade + Profile feature)

---

## 1. Blazor WASM Hosted upgrade pattern (8→10)

**When to apply:** Upgrading a Hosted Blazor WebAssembly solution from .NET 8 to .NET 10.

**Steps:**
1. Update `<TargetFramework>net10.0</TargetFramework>` in all three `.csproj` files.
2. Update NuGet packages to `10.0.7`:
   - `RuckR.Server.csproj`: `Microsoft.AspNetCore.Components.WebAssembly.Server` → `10.0.7`
   - `RuckR.Client.csproj`: `Microsoft.AspNetCore.Components.WebAssembly` → `10.0.7`
   - `RuckR.Client.csproj`: `Microsoft.AspNetCore.Components.WebAssembly.DevServer` → `10.0.7` (PrivateAssets=all)
3. Server `Program.cs`: Replace `app.UseStaticFiles()` with `app.MapStaticAssets()`. Chain `.WithStaticAssets()` on `app.MapRazorPages()` and `app.MapControllers()`.
4. Client `App.razor`: Replace `<NotFound>` RenderFragment with `NotFoundPage` parameter on `Router`. Create new `NotFound.razor` page containing the extracted content.
5. Client `wwwroot/index.html`: Update service worker registration to `navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' })`.

**Anti-pattern:** Skipping the `NotFound.razor` extraction — the `<NotFound>` RenderFragment is removed in .NET 10 Router. Leaving it in causes a compile error about Router having one too many children.

**Context:** Applies to ASP.NET Core Hosted Blazor WASM (`Microsoft.NET.Sdk.Web` for Server, `Microsoft.NET.Sdk.BlazorWebAssembly` for Client, `Microsoft.NET.Sdk` for Shared). TFM and NuGet versions must match exactly; upgrade atomically.

---

## 2. Profile feature pattern in Blazor WASM Hosted

**When to apply:** Adding a new CRUD resource (model + controller + page) to a Hosted Blazor WASM project.

**Steps:**
1. **Model** (`RuckR.Shared.Models`): C# class with DataAnnotations. Keep in `Models/` subdirectory; match existing namespace convention.
2. **Controller** (`RuckR.Server.Controllers`): `[ApiController]` + `[Route("[controller]")]`, inherit `ControllerBase`, inject `ILogger<T>` via constructor, use `private static` field for in-memory storage.
3. **Page** (`RuckR.Client.Pages`): `@page "/route"`, `@inject HttpClient Http`, `OnInitializedAsync` fetches via `GetFromJsonAsync<T>("route")`. Edit mode uses `<EditForm>` + `<DataAnnotationsValidator />`. Save sends via `PutAsJsonAsync`. Loading state: `null` check. Error state: try/catch with error message variable.

**Anti-pattern:** Creating a separate service/repository layer for single-resource in-memory storage. Follow the established `WeatherForecastController` pattern — static field in controller is sufficient.

**Context:** Based on existing `WeatherForecastController` + `FetchData.razor` pattern. Block-scoped namespaces (not file-scoped) per WeatherForecastController. Controller uses `Microsoft.AspNetCore.Mvc`; page uses `GetFromJsonAsync` / `PutAsJsonAsync` from `System.Net.Http.Json`.

---

## 3. NuGet update via dotnet CLI

**When to apply:** Updating NuGet packages in a .NET solution without external tooling.

**Steps:**
1. Set versions manually in `.csproj` files (or use `dotnet package update <package> --file <csproj>` — built-in SDK 10).
2. Run `dotnet restore` to resolve new versions.
3. Run `dotnet list package --outdated` to verify no outdated packages remain.

**Anti-pattern:** Using `dotnet package add` to upgrade (which may add duplicate references). Use `dotnet package update` or edit `.csproj` directly.

**Context:** .NET 10 SDK includes `dotnet package update` built-in. Auto-referenced SDK packages (`ILLink.Tasks`, `WebAssembly.Pack`) are SDK-managed and should not be modified — they will appear in `dotnet list package` but are not user-controllable.
