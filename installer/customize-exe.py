#!/usr/bin/env python3
"""
Tauscht das Icon einer Windows-EXE aus.
Laeuft nativ auf Linux — kein Wine noetig.

Voraussetzung:
    pip install lief

Verwendung:
    python3 customize-exe.py template.exe kunde-icon.ico output.exe

Beispiel in Laravel:
    exec("python3 customize-exe.py '$template' '$icon' '$output'");
"""

import sys
import os
import struct

try:
    import lief
except ImportError:
    print("Fehler: LIEF Library nicht installiert.", file=sys.stderr)
    print("Installieren mit: pip install lief", file=sys.stderr)
    sys.exit(1)


def replace_icon(template_path: str, icon_path: str, output_path: str):
    """Ersetzt das Icon einer EXE-Datei."""

    if not os.path.exists(template_path):
        print(f"Fehler: Template-EXE nicht gefunden: {template_path}", file=sys.stderr)
        sys.exit(1)

    if not os.path.exists(icon_path):
        print(f"Fehler: Icon-Datei nicht gefunden: {icon_path}", file=sys.stderr)
        sys.exit(1)

    # EXE laden
    pe = lief.PE.parse(template_path)
    if pe is None:
        print(f"Fehler: Konnte EXE nicht laden: {template_path}", file=sys.stderr)
        sys.exit(1)

    # ICO-Datei lesen und Icon ersetzen
    icon_data = open(icon_path, "rb").read()
    pe.change_icon(icon_path)

    # Speichern
    builder = lief.PE.Builder(pe)
    builder.build()
    builder.write(output_path)

    size_mb = os.path.getsize(output_path) / (1024 * 1024)
    print(f"OK: Icon getauscht -> {output_path} ({size_mb:.1f} MB)")


if __name__ == "__main__":
    if len(sys.argv) != 4:
        print("Verwendung: python3 customize-exe.py <template.exe> <icon.ico> <output.exe>")
        print()
        print("Argumente:")
        print("  template.exe   Pfad zur Template-EXE (wird nicht veraendert)")
        print("  icon.ico       Pfad zur ICO-Datei des Kunden")
        print("  output.exe     Pfad fuer die angepasste EXE")
        sys.exit(1)

    replace_icon(sys.argv[1], sys.argv[2], sys.argv[3])
