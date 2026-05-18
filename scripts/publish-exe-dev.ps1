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

$argsForBash = @()
if ($AppOnly) { $argsForBash += "--app-only" }
if ($NoRestore) { $argsForBash += "--no-restore" }
if ($SkipRestart) { $argsForBash += "--skip-restart" }
if ($Yes) { $argsForBash += "--yes" }
if (-not [string]::IsNullOrWhiteSpace($Ref)) {
    $argsForBash += "--ref"
    $argsForBash += $Ref
}

function Quote-BashArg {
    param([string]$Value)

    return "'" + $Value.Replace("'", "'\''") + "'"
}

function ConvertTo-WslPath {
    param([string]$WindowsPath)

    if ($WindowsPath -match '^([A-Za-z]):\\(.*)$') {
        $drive = $Matches[1].ToLowerInvariant()
        $path = $Matches[2].Replace('\', '/')
        return "/mnt/$drive/$path"
    }

    return $WindowsPath.Replace('\', '/')
}

$wsl = Get-Command wsl -ErrorAction SilentlyContinue
if ($wsl) {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
    $wslRepoRoot = ConvertTo-WslPath $repoRoot
    if ([string]::IsNullOrWhiteSpace($wslRepoRoot)) {
        throw "Unable to resolve WSL path for $repoRoot."
    }

    $quotedArgs = ($argsForBash | ForEach-Object { Quote-BashArg $_ }) -join " "
    $command = @"
cd $(Quote-BashArg $wslRepoRoot) &&
tr -d '\r' < scripts/publish-exe-dev.sh > scripts/.publish-exe-dev.tmp.sh &&
chmod +x scripts/.publish-exe-dev.tmp.sh
./scripts/.publish-exe-dev.tmp.sh $quotedArgs
code=`$?
rm -f scripts/.publish-exe-dev.tmp.sh
exit `$code
"@

    & $wsl.Source bash -lc $command
    exit $LASTEXITCODE
}

$bash = Get-Command bash -ErrorAction SilentlyContinue
if (-not $bash) {
    throw "bash or WSL is required. Run scripts/publish-exe-dev.sh from Git Bash/WSL, or install bash on PATH."
}

& $bash.Source $scriptPath @argsForBash
exit $LASTEXITCODE
