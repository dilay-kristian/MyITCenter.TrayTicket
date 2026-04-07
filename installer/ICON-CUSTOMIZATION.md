# Icon pro Kunde anpassen

## Einrichtung auf dem Linux-Server (einmalig)

```bash
pip install lief
```

## Template-EXE bereitstellen

Die fertig gebaute EXE einmal auf den Server kopieren:

```bash
mkdir -p /opt/myitcenter/tray-tool
cp MyitCenter.TrayTicketTool.exe /opt/myitcenter/tray-tool/template.exe
cp customize-exe.py /opt/myitcenter/tray-tool/
```

## Icon tauschen (Kommandozeile)

```bash
python3 /opt/myitcenter/tray-tool/customize-exe.py \
    /opt/myitcenter/tray-tool/template.exe \
    /path/to/kunde-icon.ico \
    /tmp/KundeXY_TrayTicketTool.exe
```

## Integration in Laravel

```php
// In einem Controller oder Service:

public function downloadTrayTool(Customer $customer)
{
    $template = '/opt/myitcenter/tray-tool/template.exe';
    $script = '/opt/myitcenter/tray-tool/customize-exe.py';
    $icon = storage_path("app/customer-icons/{$customer->id}.ico");
    $output = storage_path("app/temp/{$customer->id}_TrayTicketTool.exe");

    // Fallback: Template ohne Anpassung wenn kein Icon vorhanden
    if (!file_exists($icon)) {
        return response()->download($template, 'MyitCenter.TrayTicketTool.exe');
    }

    // Icon tauschen
    $result = exec("python3 '$script' '$template' '$icon' '$output' 2>&1", $out, $code);

    if ($code !== 0) {
        Log::error("Icon-Customization fehlgeschlagen", ['output' => implode("\n", $out)]);
        return response()->download($template, 'MyitCenter.TrayTicketTool.exe');
    }

    return response()->download($output, 'MyitCenter.TrayTicketTool.exe')
        ->deleteFileAfterSend(true);
}
```

## Icon-Anforderungen

- Format: `.ico`
- Empfohlene Groessen im ICO enthalten: 16x16, 32x32, 48x48, 256x256
- Tools zum Erstellen: https://icoconvert.com oder GIMP
