# Gotchas — plan 20260505-update-project

> Saved: 2026-05-05T22:26:00Z | Source: task-implementer (RuckR .NET 10 upgrade + Profile feature)

---

## 1. NETSDK1057 informational messages with .NET 10 preview SDK

**Symptom:** Build output shows `NETSDK1057` messages when building with .NET 10 SDK `10.0.300-preview`.

**Root cause:** Use of a preview .NET 10 SDK. These are **informational messages**, not warnings or errors.

**Fix:** No action needed. These are not build-blocking. If desired, upgrade to the stable .NET 10 SDK when available.

---

## 2. Router NotFoundPage requires @using in _Imports.razor

**Symptom:** Compile error — `NotFoundPage="@typeof(NotFound)"` cannot resolve `NotFound` type.

**Root cause:** The new `NotFound.razor` page is in `RuckR.Client.Pages` namespace, but Router in `App.razor` may not auto-resolve it. The `_Imports.razor` file must include a `@using` for the namespace.

**Fix:** Add `@using RuckR.Client.Pages` to `RuckR/Client/_Imports.razor` if not already present. The Router's `NotFoundPage` parameter requires the type to be in scope at compile time.

---

## 3. TFM and NuGet package versions must match exactly

**Symptom:** Package restore fails with `NU1101` or similar — "package X 10.0.7 is not compatible with net10.0".

**Root cause:** If `.csproj` TFM is `net8.0` but NuGet references are `10.0.7`, version mismatch blocks restore. Conversely, TFM `net10.0` with `8.0.0` packages causes the same issue.

**Fix:** Upgrade TFM and package versions atomically in the same wave. Never upgrade one without the other. Use `dotnet list package --outdated` and `dotnet restore` to verify after changes.

---

## 4. ProfileModel namespace: RuckR.Shared.Models (not RuckR.Shared)

**Symptom:** If ProfileModel is moved to `RuckR.Shared` namespace, it creates inconsistency with the directory structure and any existing references.

**Root cause:** The existing `ProfileModel.cs` stub lives in `RuckR/Shared/Models/` and uses `namespace RuckR.Shared.Models`. The `WeatherForecast.cs` model uses `namespace RuckR.Shared` directly (in `RuckR/Shared/`). These are intentionally different conventions.

**Fix:** Do **not** move ProfileModel. Keep it in `RuckR.Shared.Models` namespace. Client pages reference it via `@using RuckR.Shared.Models`.

---

## 5. WeatherForecastController uses block-scoped namespace

**Symptom:** If creating a new controller with file-scoped namespace (`namespace RuckR.Server.Controllers;`), it will be inconsistent with the existing `WeatherForecastController.cs` which uses block-scoped (`namespace RuckR.Server.Controllers { ... }`).

**Root cause:** The existing codebase convention (established by `WeatherForecastController.cs`) is block-scoped namespaces. File-scoped namespaces are valid C# 10+ but break project-wide consistency.

**Fix:** Match the `WeatherForecastController` style — use block-scoped namespace with opening `{` on the same line, closing `}` after the class. All new controllers should follow this convention.
