---
name: dotnet-coding-standards
description: >-
  C# / .NET / Blazor coding standards for the RuckR project. Covers naming
  conventions, formatting, C# idioms, XML doc comments, Blazor patterns, API
  design, Fluxor state management, and common anti-patterns. Based on
  Microsoft .NET guidelines and community consensus. USE FOR: writing new C#
  code, reviewing PRs, onboarding contributors, enforcing consistency across
  Server/Client/Shared projects. DO NOT USE FOR: performance analysis (use
  analyzing-dotnet-performance), debugging WASM issues (use
  blazor-wasm-troubleshooting), package management (use msbuild-antipatterns).
license: MIT
compatibility: opencode
metadata:
  framework: Blazor/.NET 10
  language: C#
  tfm: net10.0
  projects:
    - RuckR.Server
    - RuckR.Client
    - RuckR.Shared
---

# RuckR .NET / Blazor Coding Standards

Community-consensus coding standards for the RuckR Blazor WASM project.
All conventions target `.NET 10` with `net10.0` TFM and nullable reference types enabled.

---

## 1. Naming Conventions

### General C# Rules (Community Consensus)

| Kind | Convention | Example |
|------|-----------|---------|
| Namespace | PascalCase, dot-separated | `RuckR.Server.Controllers` |
| Class / Record | PascalCase | `ProfileController`, `PlayerModel` |
| Interface | PascalCase, prefixed with `I` | `IProfileService` |
| Method | PascalCase | `GetProfileAsync()`, `LoadPlayers()` |
| Property | PascalCase | `IsEditing`, `ErrorMessage` |
| Field (private) | camelCase, prefixed with `_` | `_logger`, `_profileService` |
| Local variable | camelCase | `profile`, `players` |
| Constant | PascalCase | `MaxNameLength = 100` |
| Enum type | PascalCase | `PlayerStatus` |
| Enum member | PascalCase | `Active`, `Injured` |
| Parameter | camelCase | `id`, `playerModel` |
| Type parameter (generic) | PascalCase, prefixed with `T` | `TResponse`, `TEntity` |
| Boolean | Prefix with `Is`, `Has`, `Can`, or `Should` | `IsEditing`, `HasPermission` |
| Event | Past tense or `...ing` suffix | `PlayerSelected`, `Saving` |

### Async Suffix

- Methods returning `Task`/`ValueTask` that are async: suffix with `Async`
  ```csharp
  public async Task<PlayerModel> GetPlayerAsync(int id) { ... }
  ```
- Synchronous methods: **no** `Async` suffix
  ```csharp
  public PlayerModel GetPlayer(int id) { ... }
  ```

### Nullable Reference Types

- All three projects have `<Nullable>enable</Nullable>`
- Use `?` for nullable reference types: `string?`, `PlayerModel?`
- Use `!` null-forgiving operator **only** when you can prove non-null
- Prefer default values for non-nullable properties:
  ```csharp
  public string Name { get; set; } = string.Empty;
  public int Score { get; set; } = 0;
  ```

---

## 2. Namespaces

### Block-Scoped Namespaces (Project Convention)

Use **block-scoped** namespaces (not file-scoped) for consistency:

```csharp
// ✅ Correct (block-scoped — used in this project)
namespace RuckR.Server.Controllers
{
    public class ProfileController : ControllerBase { ... }
}

// ❌ Avoid (file-scoped — not used in this project)
namespace RuckR.Server.Controllers;
```

### Namespace Mapping

| Directory | Namespace |
|-----------|-----------|
| `RuckR/Server/` | `RuckR.Server` |
| `RuckR/Server/Controllers/` | `RuckR.Server.Controllers` |
| `RuckR/Shared/` | `RuckR.Shared` |
| `RuckR/Shared/Models/` | `RuckR.Shared.Models` |
| `RuckR/Client/Pages/` | `RuckR.Client.Pages` |
| `RuckR/Client/Components/` | `RuckR.Client.Components` |
| `RuckR/Server/Services/` | `RuckR.Server.Services` |
| `RuckR/Client/Store/` | `RuckR.Client.Store` |

---

## 3. Formatting & Layout

### Braces

- **Allman-style braces** for classes, methods, control flow:
  ```csharp
  // ✅
  if (condition)
  {
      DoSomething();
  }

  // ✅
  public class PlayerService
  {
      public void Load() { ... }
  }

  // ❌
  if (condition) DoSomething();
  ```

- Exception: Property/getter/setter bodies on a single line when trivial:
  ```csharp
  public string Name { get; set; } = string.Empty;
  ```

### Indentation

- Use **4 spaces** (no tabs)
- `using` directives inside namespace when block-scoped

### Blank Lines

- One blank line between methods
- One blank line between properties
- Two blank lines between class/record definitions

### Line Length

- Soft limit at 100 characters; wrap when practical

---

## 4. C# Idioms & Best Practices

### Strings

- Use **string interpolation** over `string.Format` or concatenation:
  ```csharp
  // ✅
  var message = $"Player {name} scored {score} points.";

  // ❌
  var message = string.Format("Player {0} scored {1} points.", name, score);
  ```

- Use **raw string literals** for multi-line or JSON-like strings (C# 11+):
  ```csharp
  var json = """
      {
          "name": "Alice",
          "score": 42
      }
      """;
  ```

### Collections

- Prefer `List<T>` for mutable collections
- Prefer `IReadOnlyList<T>` / `IReadOnlyCollection<T>` for return types
- Use `Array.Empty<T>()` instead of `new T[0]`
- Use `ImmutableArray<T>` for thread-safe fixed collections

### Pattern Matching

- Use `switch` expressions over `switch` statements where possible:
  ```csharp
  var label = status switch
  {
      PlayerStatus.Active => "Ready",
      PlayerStatus.Injured => "Out",
      _ => "Unknown"
  };
  ```

- Use property patterns and `is` patterns:
  ```csharp
  if (model is { Name: not null, Score: > 0 }) { ... }
  ```

### Records

- Use `record` for immutable data transfer objects and state:
  ```csharp
  public record PlayerState(string Name, int Score, Point? Location);
  ```

- Use `sealed record class` if reference equality is needed:
  ```csharp
  public sealed record class PlayerModel(string Name, int Score);
  ```

### Error Handling

- Throw specific exception types, not bare `Exception`
- Use `ArgumentNullException.ThrowIfNull()` for parameter validation:
  ```csharp
  public void SetName(string name)
  {
      ArgumentNullException.ThrowIfNull(name);
      Name = name;
  }
  ```

- Avoid catching `System.Exception` except at top-level middleware

### Dependency Injection

- Prefer constructor injection over property injection
- Use `readonly` fields for injected services:
  ```csharp
  private readonly ILogger<ProfileController> _logger;

  public ProfileController(ILogger<ProfileController> logger)
  {
      _logger = logger;
  }
  ```

- Register services in `Program.cs` with appropriate lifetimes:
  - `AddSingleton` for shared state
  - `AddScoped` for per-request/per-component state
  - `AddTransient` for stateless services

### Async

- **Always** use `async`/`await` for I/O-bound work; never use `.Result` or `.Wait()`
- Return `Task` from async methods, never `void` (except Blazor event handlers)
- Use `CancellationToken` where available
- Use `ValueTask<T>` for high-performance hot paths

### Data Annotations

- Apply validation attributes on model properties:
  ```csharp
  [Required(ErrorMessage = "Name is required.")]
  [MaxLength(100)]
  public string Name { get; set; } = string.Empty;

  [Range(0, 1000)]
  public int Score { get; set; } = 0;

  [EmailAddress]
  public string Email { get; set; } = string.Empty;
  ```

---

## 5. XML Documentation Comments

### When to Document

- **All `public` members**: methods, properties, constructors, indexers, events, delegates, classes, records, structs, interfaces, enums
- **`protected` and `internal` members**: document if they are part of the library's API surface (i.e., designed to be overridden or called by consumers)
- **`private` / `private protected`**: optional — document only if the logic is complex or non-obvious

### Summary (`/// <summary>`)

Every documented member **must** have a `<summary>` block. Start with a capital letter, end **without** a period.

```csharp
/// <summary>
/// Retrieves the player profile for the currently authenticated user
/// </summary>
public async Task<ActionResult<UserProfileModel>> Get() { ... }
```

For `async` methods, prefix with "Asynchronously ...":

```csharp
/// <summary>
/// Asynchronously loads all active players from the database
/// </summary>
public async Task<List<PlayerModel>> LoadActivePlayersAsync() { ... }
```

For properties, use a noun phrase:

```csharp
/// <summary>
/// The display name of the player
/// </summary>
public string Name { get; set; } = string.Empty;
```

### Parameters (`/// <param>`)

Use `<param name="paramName">` for every parameter. Description starts lowercase, no period.

```csharp
/// <summary>
/// Calculates the distance between two players on the pitch
/// </summary>
/// <param name="playerA">The first player position</param>
/// <param name="playerB">The second player position</param>
/// <returns>The distance in meters</returns>
public double CalculateDistance(Point playerA, Point playerB) { ... }
```

### Return Value (`/// <returns>`)

Describe what the method returns. Start lowercase, no period.

```csharp
/// <summary>
/// Finds a player by their unique identifier
/// </summary>
/// <param name="playerId">The unique player identifier</param>
/// <returns>The player model, or null if not found</returns>
public async Task<PlayerModel?> FindPlayerAsync(int playerId) { ... }
```

### Exceptions (`/// <exception>`)

Document exceptions that callers should handle. Use `cref` to reference the exception type.

```csharp
/// <summary>
/// Saves the updated player profile
/// </summary>
/// <param name="model">The updated profile model</param>
/// <exception cref="ArgumentNullException">Thrown when model is null</exception>
/// <exception cref="DbUpdateException">Thrown when the database update fails</exception>
public async Task SaveProfileAsync(PlayerModel model) { ... }
```

### Type Parameters (`/// <typeparam>`)

Document generic type parameters on generic classes and methods.

```csharp
/// <summary>
/// A generic repository for player entities
/// </summary>
/// <typeparam name="TEntity">The entity type managed by this repository</typeparam>
public class PlayerRepository<TEntity> where TEntity : class { ... }
```

### Example Blocks (`/// <example>`)

Use for complex APIs or extension methods where usage is not obvious.

```csharp
/// <summary>
/// Returns only players that match the specified status filter
/// </summary>
/// <param name="query">The source query</param>
/// <param name="status">The status to filter by</param>
/// <returns>A filtered queryable</returns>
/// <example>
/// <code>
/// var activePlayers = await dbContext.Players
///     .FilterByStatus(PlayerStatus.Active)
///     .ToListAsync();
/// </code>
/// </example>
public static IQueryable<PlayerModel> FilterByStatus(
    this IQueryable<PlayerModel> query,
    PlayerStatus status) { ... }
```

### Controller XML Docs

All controller actions must have XML doc comments. The IDE and Swagger/OpenAPI tooling use them to generate API documentation.

```csharp
/// <summary>
/// Gets the profile for the currently authenticated user
/// </summary>
/// <returns>The user's profile model</returns>
/// <response code="200">Returns the profile successfully</response>
/// <response code="401">If the user is not authenticated</response>
[HttpGet]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<ActionResult<UserProfileModel>> Get() { ... }
```

### Doctrinable Principles

- Keep summaries concise (one line when possible)
- Use `<para>` for multi-paragraph descriptions:
  ```csharp
  /// <summary>
  /// Initializes a new instance of the <see cref="PlayerService"/> class.
  /// </summary>
  /// <para>
  /// This constructor registers all required dependencies. The logger
  /// is used for diagnostic output during player operations.
  /// </para>
  ```
- Use `<see cref="T:TypeName"/>` for cross-references (not raw type names)
- Use `<inheritdoc/>` when overriding inherited members:
  ```csharp
  /// <inheritdoc/>
  public override string ToString() => Name;
  ```

---

## 6. Blazor-Specific Standards

### Razor Directive Ordering

Order directives at the top of `.razor` files as follows:
1. `@page`
2. `@rendermode`
3. `@using`
4. `@attribute`
5. `@implements`
6. `@inject`

```razor
@page "/profile"
@rendermode InteractiveServer
@using RuckR.Shared.Models
@attribute [Authorize]
@implements IAsyncDisposable
@inject HttpClient Http
@inject ILogger<Profile> Logger
```

### Render Modes

| Scenario | Render Mode |
|----------|-------------|
| Static content / SEO pages | Static SSR (default) |
| Interactive server components | `InteractiveServer` |
| Offline-capable client interactivity | `InteractiveWebAssembly` |
| Progressive enhancement | `InteractiveAuto` |

### Component Architecture

- Use **code-behind** (`*.razor.cs`) for components with significant logic
- Keep `.razor` files focused on markup and minimal binding
- Use `@code` blocks only for trivial logic (5 lines or fewer)
- Components should be `partial class`:
  ```csharp
  // Profile.razor.cs
  public sealed partial class Profile : IAsyncDisposable
  {
      // ...
  }
  ```

### State Management — Fluxor

- Each feature gets its own directory under `Store/`:
  ```
  Store/
    Location/
      LocationState.cs
      LocationActions.cs
      LocationReducers.cs
    Profile/
      ProfileState.cs
      ProfileActions.cs
      ProfileReducers.cs
  ```

- State classes use the `[FeatureState]` attribute and are `record` types:
  ```csharp
  [FeatureState]
  public record LocationState
  {
      public double? UserLatitude { get; init; }
      public double? UserLongitude { get; init; }
      public string? ErrorMessage { get; init; }
      public LocationState() { }  // Required by Fluxor
  }
  ```

- Actions are plain classes or records:
  ```csharp
  public class StartLocationWatchingAction { }
  public class LocationUpdatedAction(double latitude, double longitude) { }
  ```

### SignalR

- Hub classes inherit from `Hub` and live in `RuckR.Server.Hubs`
- Register hubs in `Program.cs` with `app.MapHub<THub>("/<path>")`
- Client connections use `IHubConnectionBuilder` in Blazor components
- Always dispose hub connections in `DisposeAsync`

### JS Interop

- Use `IJSObjectReference` for module-scoped JS (preferred over global calls)
- Load modules in `OnAfterRenderAsync(firstRender: true)` only
- Always dispose module references:
  ```csharp
  private IJSObjectReference? _module;

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
      if (firstRender)
      {
          _module = await JS.InvokeAsync<IJSObjectReference>(
              "import", "./scripts/myModule.js");
      }
  }

  public async ValueTask DisposeAsync()
  {
      if (_module is not null)
          await _module.DisposeAsync();
  }
  ```

### Forms & Validation

- Always use `<EditForm>` with `<DataAnnotationsValidator />` and `<ValidationSummary />`:
  ```razor
  <EditForm Model="@model" OnValidSubmit="@HandleValidSubmit" FormName="edit-profile">
      <DataAnnotationsValidator />
      <ValidationSummary class="text-danger" />
      <InputText @bind-Value="model.Name" />
      <ValidationMessage For="() => model.Name" />
      <button type="submit">Save</button>
  </EditForm>
  ```

- Use the `Model` property setter to create a fresh copy on each submit to avoid stale references.

### Routing

- All Razor pages use `@page` with kebab-case routes:
  ```razor
  @page "/player-collection"
  @page "/battles/lobby"
  ```

- Route parameters use inline constraints:
  ```razor
  @page "/players/{playerId:int}"
  ```

- The `<Router>` uses `NotFoundPage` (not `<NotFound>`) per .NET 10 convention:
  ```razor
  <Router AppAssembly="typeof(Program).Assembly"
           NotFoundPage="typeof(NotFound)">
  ```

### CSS Isolation

- Every component has an accompanying `.razor.css` file
- Global styles live in `wwwroot/css/app.css`
- Never use global styles to override component-scoped elements

---

## 7. Controller / API Standards

### Controller Pattern

All API controllers follow this template:

```csharp
namespace RuckR.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            IProfileService profileService,
            UserManager<IdentityUser> userManager,
            ILogger<ProfileController> logger)
        {
            _profileService = profileService;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<UserProfileModel>> Get() { ... }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] UserProfileModel model) { ... }
    }
}
```

### Controller Rules

| Rule | Detail |
|------|--------|
| Route template | `api/[controller]` (lowercase, kebab-case) |
| Base class | `ControllerBase` |
| Auth | `[Authorize]` on the class; override per-action if needed |
| Logger | `ILogger<T>` injected via constructor |
| Return types | `ActionResult<T>` for GET, `IActionResult` for writes |
| Body binding | `[FromBody]` on DTO parameters |
| Async | All data-fetching methods are `async Task` |
| Naming | `XxxController` suffix |

### Model / DTO Separation

- **Domain models** (`RuckR.Shared.Models`): Rich objects with DataAnnotations, used for client–server transfer
- **Database entities** (`RuckR.Server.Data`): EF Core entities with spatial types, timestamps, navigation properties
- **DTOs** (`RuckR.Shared`): Use `sealed record` types for explicit request/response contracts

---

## 8. Security Standards

### Secrets Management

| Environment | Method | Example |
|-------------|--------|---------|
| Local dev | `dotnet user-secrets` | `dotnet user-secrets set "ConnectionStrings:RuckRDbContext" "..."` |
| Deployment | Environment variable | `RUCKR_DB_PASSWORD` |
| Tests | Auto-generated GUID fallback | `Guid.NewGuid().ToString("N")` |

- **Never** commit passwords, connection strings, or API keys to source control
- `.gitignore` must cover `*.env`, `secrets.*`, `appsettings.*.local.json`

### Data Protection

- Use `[ValidateAntiForgeryToken]` on state-changing POST actions (when applicable)
- Sanitize all user input; use EF Core parameterized queries (never raw SQL concatenation)
- Hash passwords with ASP.NET Core Identity's built-in hasher (PBKDF2)

---

## 9. Spatial Data Standards (ADR-004)

- Use SQL Server `geography` type with NetTopologySuite
- SRID 4326 (WGS 84) for all GPS coordinates
- NTS convention: `Point(longitude, latitude)` — **X = longitude, Y = latitude**
- Always set SRID explicitly:
  ```csharp
  var point = new Point(longitude, latitude) { SRID = 4326 };
  ```
- Use `GeometryEngine` for spatial operations (distance, buffer, intersection)

---

## 10. Common Anti-Patterns

| Anti-Pattern | Why It's Wrong | Fix |
|-------------|----------------|-----|
| File-scoped namespaces | Inconsistent with project convention | Use block-scoped `namespace X { }` |
| `var` everywhere | Reduces readability for complex types | Use explicit types when the type isn't obvious from the right side |
| `async void` | Unhandled exceptions crash the process | Use `async Task` always (except Blazor event handlers) |
| `.Result` / `.Wait()` | Causes deadlocks in ASP.NET context | Use `await` |
| Catching `Exception` | Masks all errors | Catch specific exception types |
| Null checks with `!= null` | Verbose, error-prone | Use null-coalescing `??`, pattern matching, or NRTs |
| Hardcoded connection strings | Security risk, non-portable | Use `IConfiguration` + user-secrets/env vars |
| Inline JavaScript in `.razor` | Hard to maintain, breaks isolation | Use `IJSObjectReference` + separate `.js` modules |
| Logic in `.razor` markup beyond binding | Hard to test, violates separation | Move logic to code-behind (`*.razor.cs`) |
| Missing XML doc on public members | Harms discoverability and API docs | Add `/// <summary>` + `<param>`/`<returns>` to all public members |

---

## 11. Validation Checklist (PR Review)

Before approving a PR, verify:

- [ ] Nullable reference types respected (`string?` vs `string`)
- [ ] Block-scoped namespaces used consistently
- [ ] Async methods return `Task`/`ValueTask`, not `void`
- [ ] DataAnnotations applied on model properties
- [ ] No hardcoded secrets or connection strings
- [ ] Controller follows `[ApiController]` + `[Route]` + `ControllerBase` pattern
- [ ] Spatial data uses SRID 4326 and correct longitude/latitude order
- [ ] Fluxor actions are plain classes/records; state uses `[FeatureState]`
- [ ] Blazor components use code-behind (`.razor.cs`) for non-trivial logic
- [ ] Forms use `<EditForm>` + `<DataAnnotationsValidator />`
- [ ] JS interop uses `IJSObjectReference` and proper disposal
- [ ] No `.Result` / `.Wait()` anti-patterns
- [ ] Naming follows PascalCase (public) / camelCase (private/param) conventions
- [ ] `ILogger<T>` used for logging, not `Console.WriteLine`
- [ ] All `public` members have XML doc comments (`/// <summary>`, `<param>`, `<returns>`)
- [ ] Build passes with zero warnings treated as errors
