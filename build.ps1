<#
.SYNOPSIS
    Baut das myit.center Ticket Tool als Release-Build.
    Ausgabe in ./publish/
#>

$ErrorActionPreference = "Stop"
$ProjectDir = "$PSScriptRoot\src\MyitCenter.TrayTicketTool"
$PublishDir = "$PSScriptRoot\publish"

Write-Host "=== myit.center Ticket Tool - Build ===" -ForegroundColor Cyan

# Clean
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

# Publish als self-contained single-file
# Trimming nicht moeglich wegen WinForms (NotifyIcon/Tray)
# ~155 MB ist normal - enthaelt .NET Runtime + WPF + WinForms
dotnet publish $ProjectDir `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build fehlgeschlagen!"
    exit 1
}

Write-Host ""
Write-Host "Build erfolgreich! Ausgabe in: $PublishDir" -ForegroundColor Green
Write-Host "Dateien:" -ForegroundColor Cyan
Get-ChildItem $PublishDir | Format-Table Name, @{N="Size (MB)";E={[math]::Round($_.Length / 1MB, 1)}} -AutoSize
