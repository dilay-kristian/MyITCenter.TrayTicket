<#
.SYNOPSIS
    Deployment-Script fuer das myit.center Ticket Tool.
    Kann vom ITDokuAgent oder manuell ausgefuehrt werden.

.DESCRIPTION
    Kopiert das Ticket Tool nach Program Files und richtet den Autostart
    fuer alle Benutzer ein (Common Startup Folder).

.PARAMETER SourcePath
    Pfad zum Ordner mit den publizierten Dateien.

.PARAMETER Uninstall
    Entfernt das Ticket Tool.

.EXAMPLE
    .\Deploy-TrayTicketTool.ps1 -SourcePath "C:\temp\TrayTicketTool"

.EXAMPLE
    .\Deploy-TrayTicketTool.ps1 -Uninstall
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$SourcePath,

    [switch]$Uninstall
)

$InstallDir = "$env:ProgramFiles\MyitCenter\TrayTicketTool"
$ExePath = "$InstallDir\MyitCenter.TrayTicketTool.exe"
$StartupFolder = [Environment]::GetFolderPath("CommonStartup")
$ShortcutPath = "$StartupFolder\myit.center Ticket Tool.lnk"

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message"
}

# --- Uninstall ---
if ($Uninstall) {
    Write-Log "Deinstallation gestartet..."

    # Prozess beenden
    Get-Process -Name "MyitCenter.TrayTicketTool" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1

    # Shortcut entfernen
    if (Test-Path $ShortcutPath) {
        Remove-Item $ShortcutPath -Force
        Write-Log "Autostart-Verknuepfung entfernt."
    }

    # Dateien entfernen
    if (Test-Path $InstallDir) {
        Remove-Item $InstallDir -Recurse -Force
        Write-Log "Installationsverzeichnis entfernt."
    }

    Write-Log "Deinstallation abgeschlossen."
    exit 0
}

# --- Install ---
if (-not $SourcePath) {
    Write-Error "SourcePath muss angegeben werden."
    exit 1
}

if (-not (Test-Path $SourcePath)) {
    Write-Error "Quellverzeichnis '$SourcePath' nicht gefunden."
    exit 1
}

Write-Log "Installation gestartet..."

# Laufende Instanz beenden
Get-Process -Name "MyitCenter.TrayTicketTool" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Zielverzeichnis erstellen und Dateien kopieren
if (!(Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

Copy-Item -Path "$SourcePath\*" -Destination $InstallDir -Recurse -Force
Write-Log "Dateien nach '$InstallDir' kopiert."

# Autostart-Verknuepfung im Common Startup (fuer alle Benutzer / Terminal Server Sessions)
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($ShortcutPath)
$shortcut.TargetPath = $ExePath
$shortcut.WorkingDirectory = $InstallDir
$shortcut.Description = "myit.center Ticket Tool"
$shortcut.Save()
Write-Log "Autostart-Verknuepfung erstellt: $ShortcutPath"

# Starten fuer aktuelle Session
Start-Process -FilePath $ExePath
Write-Log "Ticket Tool gestartet."

Write-Log "Installation abgeschlossen."
