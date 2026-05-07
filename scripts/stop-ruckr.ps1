<#
.SYNOPSIS
    Stops the RuckR development environment.
#>
Write-Host "Stopping RuckR Server..." -ForegroundColor Cyan
Stop-Job -Name RuckRServer -ErrorAction SilentlyContinue
Remove-Job -Name RuckRServer -ErrorAction SilentlyContinue

Write-Host "Stopping Aspire Dashboard..." -ForegroundColor Cyan
docker rm -f ruckr-aspire-dashboard 2>$null

Write-Host "Done." -ForegroundColor Green
