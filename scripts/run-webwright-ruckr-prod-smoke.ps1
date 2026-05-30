param(
    [string]$WebwrightRoot = "$env:USERPROFILE\source\repos\Webwright",
    [string]$StartUrl = "https://ruckr.exe.xyz",
    [string]$TaskId = "ruckr-prod-smoke",
    [string]$ModelConfig = "model_openai.yaml",
    [string]$OutputDir = "$PSScriptRoot\..\.artifacts\webwright"
)

$ErrorActionPreference = "Stop"

$python = Join-Path $WebwrightRoot ".venv\Scripts\python.exe"

if (-not (Test-Path $python)) {
    throw "Webwright Python environment not found at $python. Run setup from C:\Users\clock\source\repos\Webwright first."
}

$requiredKey = switch ($ModelConfig) {
    "model_openai.yaml" { "OPENAI_API_KEY"; break }
    "model_claude.yaml" { "ANTHROPIC_API_KEY"; break }
    "model_openrouter.yaml" { "OPENROUTER_API_KEY"; break }
    default { $null }
}

if ($requiredKey -and [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($requiredKey))) {
    throw "Set $requiredKey before running Webwright with $ModelConfig."
}

if (-not $requiredKey) {
    $hasAnyKey = (-not [string]::IsNullOrWhiteSpace($env:OPENAI_API_KEY)) `
        -or (-not [string]::IsNullOrWhiteSpace($env:ANTHROPIC_API_KEY)) `
        -or (-not [string]::IsNullOrWhiteSpace($env:OPENROUTER_API_KEY))

    if (-not $hasAnyKey) {
        throw "Set OPENAI_API_KEY, ANTHROPIC_API_KEY, or OPENROUTER_API_KEY before running the Webwright CLI agent."
    }
}

$task = @"
Verify the RuckR production app end-to-end without mutating production data unless the UI requires a harmless read-only interaction.

Critical points:
1. Load https://ruckr.exe.xyz and verify the Blazor app renders, not just the health endpoint.
2. Visit /api/telemetry/health and record the JSON health status.
3. Return to /map and verify the map route renders a visible user-facing state.
4. If browser geolocation is blocked or unavailable, verify the page explains what the user should do instead of leaving a blank map.
5. Capture console errors and network failures that are relevant to app startup, ArcGIS/GeoBlazor loading, SignalR, or CSS/static asset loading.
6. If RUCKR_PROD_SMOKE_EMAIL and RUCKR_PROD_SMOKE_PASSWORD are present in the environment, use them to verify login and then return to /map. Do not print the password.

Save screenshots and a concise final result summary.
"@

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Push-Location $WebwrightRoot
try {
    & $python -m webwright.run.cli `
        -c base.yaml `
        -c $ModelConfig `
        -t $task `
        --start-url $StartUrl `
        --task-id $TaskId `
        -o (Resolve-Path $OutputDir)
}
finally {
    Pop-Location
}
