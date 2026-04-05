# myit.center Ticket Tool - Installations- und Build-Anleitung

## Voraussetzungen (nur fuer Build)

- .NET 8.0 SDK (auf dem Entwicklungsrechner)
- Optional: [Inno Setup](https://jrsoftware.org/isdownload.php) (nur wenn ein Setup-Installer erstellt werden soll)

Auf den Zielrechnern wird **nichts** benoetigt - die EXE ist self-contained.

---

## 1. Build erstellen

### PowerShell (empfohlen)

```powershell
cd C:\Users\User\Projects\MyitCenter.TrayTicketTool
.\build.ps1
```

Ergebnis: `publish\MyitCenter.TrayTicketTool.exe` (self-contained, single-file, ~155 MB)

### Alternativ manuell

```powershell
dotnet publish src\MyitCenter.TrayTicketTool -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

---

## 2. Installation auf Zielrechnern

Es gibt drei Wege, je nach Situation:

### Option A: Inno Setup Installer (fuer manuelle Installation)

1. Build erstellen (siehe oben)
2. Inno Setup Compiler ausfuehren:
   ```
   iscc installer\setup.iss
   ```
3. Ergebnis: `installer\output\MyitCenter.TrayTicketTool_Setup.exe`
4. Diese Setup-EXE auf dem Zielrechner ausfuehren (als Admin)

Der Installer macht automatisch:
- Kopiert nach `C:\Program Files\MyitCenter\TrayTicketTool\`
- Erstellt Autostart-Verknuepfung (Common Startup, gilt fuer alle Benutzer)
- Startet die App nach der Installation

Fuer Silent-Installation (z.B. per GPO):
```
MyitCenter.TrayTicketTool_Setup.exe /VERYSILENT /SUPPRESSMSGBOXES
```

### Option B: PowerShell Deploy-Script (fuer Rollout ueber ITDokuAgent)

1. `publish\`-Ordner auf einen Netzwerkshare oder Download-Server legen
2. Auf dem Zielrechner (als Admin):
   ```powershell
   .\installer\Deploy-TrayTicketTool.ps1 -SourcePath "\\server\share\TrayTicketTool"
   ```

Das Script macht:
- Beendet laufende Instanzen
- Kopiert nach `C:\Program Files\MyitCenter\TrayTicketTool\`
- Erstellt Autostart im Common Startup Ordner
- Startet die App fuer die aktuelle Session

### Option C: Manuell (zum Testen)

Einfach die EXE irgendwo hinkopieren und starten:
```
publish\MyitCenter.TrayTicketTool.exe
```

---

## 3. Autostart

Die App wird ueber den **Common Startup Ordner** gestartet:
```
C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Startup\myit.center Ticket Tool.lnk
```

Das bedeutet:
- Die App startet bei **jedem Benutzer-Login** automatisch
- Auf **Terminal Servern** bekommt jede Session ihre eigene Instanz
- Single-Instance-Schutz verhindert doppelte Starts pro Session

---

## 4. Deinstallation

### Per Installer
Ueber "Programme und Features" in Windows oder:
```
"C:\Program Files\MyitCenter\TrayTicketTool\unins000.exe" /VERYSILENT
```

### Per Deploy-Script
```powershell
.\installer\Deploy-TrayTicketTool.ps1 -Uninstall
```

---

## 5. Update

Gleicher Ablauf wie Installation - die laufende Instanz wird automatisch beendet,
Dateien ueberschrieben und die App neu gestartet.

---

## 6. Voraussetzung auf dem Zielrechner

- **ITDokuAgent** muss installiert sein unter `C:\ProgramData\ITDokuAgent\`
- Die `config.json` muss vorhanden sein mit `api_url`, `agent_token` und `device_id`
- Ohne Agent-Config funktioniert die App im Offline-Modus (lokale Speicherung)

Tickets werden lokal unter `%LOCALAPPDATA%\MyitCenter\Tickets\` gespeichert.

---

## Verzeichnisstruktur nach Installation

```
C:\Program Files\MyitCenter\TrayTicketTool\
  MyitCenter.TrayTicketTool.exe        <- Die App

C:\ProgramData\ITDokuAgent\
  config.json                           <- Agent-Konfiguration (api_url, agent_token, device_id)

%LOCALAPPDATA%\MyitCenter\Tickets\
  {ticket-id}\
    ticket.json                         <- Ticket-Daten
    screenshot.png                      <- Screenshot
```
