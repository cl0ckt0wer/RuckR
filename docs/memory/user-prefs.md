# User Preferences

> Global preferences. Last updated: 2026-05-05T22:26:00Z

---

## Autonomy

- **Full autonomy execution** — no pauses between tasks, no confirmations for routine operations. Proceed through plan waves without asking for approval.

## Tooling

- **dotnet CLI preferred** over external tooling. Use `dotnet build`, `dotnet restore`, `dotnet list package --outdated`, `dotnet package update` over GUI tools or third-party package managers.

## Build Verification

- **Build verification after each wave.** Run `dotnet build RuckR.sln` and confirm zero errors (and ideally zero warnings) before proceeding to the next wave of changes.

## Storage

- **In-memory storage is acceptable.** No authentication, no database, no persistent storage needed. Static fields in controllers are sufficient for single-user profile scenarios.
