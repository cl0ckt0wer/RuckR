[CmdletBinding()]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]] $DotnetTestArgs
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$lockDirectory = Join-Path $repoRoot '.artifacts\locks'
$lockPath = Join-Path $lockDirectory 'dotnet-test-build.lock'

New-Item -ItemType Directory -Force -Path $lockDirectory | Out-Null

$timeout = [TimeSpan]::FromMinutes(10)
$wait = [Diagnostics.Stopwatch]::StartNew()
$lockStream = $null

while ($null -eq $lockStream) {
    try {
        $lockStream = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    }
    catch [IO.IOException] {
        if ($wait.Elapsed -gt $timeout) {
            throw "Timed out waiting for the RuckR test build lock at $lockPath."
        }

        Start-Sleep -Milliseconds 250
    }
}

$previousIsolatedBuild = $env:RuckRIsolatedBuild
$previousBuildInstance = $env:RuckRBuildInstance

try {
    if ($DotnetTestArgs.Count -eq 0) {
        $DotnetTestArgs = @('RuckR.Tests\RuckR.Tests.csproj')
    }

    $env:RuckRIsolatedBuild = 'true'
    $env:RuckRBuildInstance = [Diagnostics.Process]::GetCurrentProcess().Id.ToString()

    $arguments = @('test') + $DotnetTestArgs
    Write-Host "dotnet $($arguments -join ' ')"
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:RuckRIsolatedBuild = $previousIsolatedBuild
    $env:RuckRBuildInstance = $previousBuildInstance

    if ($null -ne $lockStream) {
        $lockStream.Dispose()
    }
}
