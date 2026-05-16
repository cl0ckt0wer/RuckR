<#
.SYNOPSIS
    Publishes RuckR to exe.dev using the canonical bash deploy script.
.DESCRIPTION
    This PowerShell entrypoint is intentionally a thin wrapper so there is one
    deploy implementation to maintain: scripts/publish-exe-dev.sh.
.EXAMPLE
    .\scripts\publish-exe-dev.ps1
    .\scripts\publish-exe-dev.ps1 -AppOnly
    .\scripts\publish-exe-dev.ps1 -NoRestore
    .\scripts\publish-exe-dev.ps1 -SkipRestart
    .\scripts\publish-exe-dev.ps1 -Ref master
#>

param(
    [switch]$AppOnly,
    [switch]$NoRestore,
    [switch]$SkipRestart,
    [switch]$Yes,
    [string]$Ref
)

$ErrorActionPreference = "Stop"
$scriptPath = Join-Path $PSScriptRoot "publish-exe-dev.sh"

$bash = Get-Command bash -ErrorAction SilentlyContinue
if (-not $bash) {
    throw "bash is required. Run scripts/publish-exe-dev.sh from Git Bash/WSL, or install bash on PATH."
}

$argsForBash = @()
if ($AppOnly) { $argsForBash += "--app-only" }
if ($NoRestore) { $argsForBash += "--no-restore" }
if ($SkipRestart) { $argsForBash += "--skip-restart" }
if ($Yes) { $argsForBash += "--yes" }
if (-not [string]::IsNullOrWhiteSpace($Ref)) {
    $argsForBash += "--ref"
    $argsForBash += $Ref
}

& $bash.Source $scriptPath @argsForBash
exit $LASTEXITCODE
