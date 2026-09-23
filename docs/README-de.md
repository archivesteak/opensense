<div align="center">

![OpenSense](../resources/banner.png)

**Lüfter-, Leistungs- und Beleuchtungssteuerung für Acer-Nitro- und -Predator-Laptops.**<br>
Ein Open-Source-Ersatz für NitroSense.

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&color=E4473C)](../LICENSE)

[**Download**](https://github.com/archivesteak/opensense/releases/latest) •
[Funktionen](#funktionen) •
[Unterstützte Laptops](#unterstützte-laptops) •
[Erstellen](#erstellen)

[English](../README.md) |
[Bahasa Indonesia](README-id.md) |
**Deutsch** |
[Español](README-es.md) |
[Français](README-fr.md) |
[Polski](README-pl.md) |
[Português (Brasil)](README-pt-BR.md) |
[Tiếng Việt](README-vi.md) |
[Türkçe](README-tr.md) |
[Русский](README-ru.md) |
[Українська](README-uk.md) |
[简体中文](README-zh-CN.md) |
[繁體中文](README-zh-TW.md)

Hilf mit, OpenSense und diese Seite in deine Sprache zu übersetzen: siehe [Übersetzungen](#übersetzungen).

</div>

## Funktionen

- **Echte Temperaturen.** CPU und GPU werden direkt von den Chips gelesen, wie es HWiNFO und ThrottleStop tun, mit Verlaufsdiagrammen über 5 Minuten.
- **Lüftersteuerung.** Auto, Max, eine feste Drehzahl pro Lüfter oder selbst gezeichnete Temperaturkurven.
- **Sicher von Haus aus.** Bei einer Notfalltemperatur laufen die Lüfter mit voller Drehzahl, und die Firmware übernimmt wieder, wenn ein Sensor oder OpenSense ausfällt.
- **Leistung.** Betriebsmodi, CoolBoost und Windows-Energiesparpläne.
- **Beleuchtung.** Feste Farben pro Tastaturzone oder die Effekte Atmen, Neon, Welle, Wandern und Zoom.
- **Tastatur und Display.** Automatisches Ausschalten der Beleuchtung, Sperre der Windows-Taste, LCD-Overdrive und der GPU-Umschalter (MUX).
- **Keine Administratorabfragen.** Ein kleiner Hintergrunddienst wendet deine Einstellungen ab dem Systemstart an, und die NitroSense-Taste öffnet die App.
- **Deine Sprache.** 36 Sprachen, wie in Windows oder in den Einstellungen gewählt.

OpenSense fragt die Firmware, was dein Laptop hat, und zeigt nur an, was er unterstützt.

## Installation

Lade die neueste Version unter [**Releases**](https://github.com/archivesteak/opensense/releases/latest) herunter (Windows 10 2004 oder neuer, 64 Bit):

- **`OpenSense-<version>-Setup-x64.exe`**: das Installationsprogramm. Es enthält alles, was OpenSense braucht, und hält sich selbst aktuell.
- **`OpenSense-<version>-Portable-x64.zip`**: ohne Installation. Fragt nach Administratorrechten und steuert den Laptop nur, solange es geöffnet ist.
  Der PawnIO-Treiber ist nicht enthalten. Bis du ihn separat über die [Releases von PawnIO](https://github.com/namazso/PawnIO.Setup/releases/latest) installierst, kommt die CPU-Temperatur aus der Firmware des Laptops und ist weniger genau.

> [!NOTE]
> Schalte NitroSense aus (oder deinstalliere es), bevor du OpenSense verwendest, sonst kämpfen die beiden um die Lüfter.

## Unterstützte Laptops

Acer-Laptops mit der Gaming-Firmware-Schnittstelle, die NitroSense und PredatorSense verwenden. OpenSense wird auf einem **Nitro 5 AN515-57** entwickelt.

Auf einem anderen Modell ausprobiert? [Erstelle ein Issue](https://github.com/archivesteak/opensense/issues) und füge die Diagnosedaten aus **Einstellungen → Problembehandlung → Kopieren** ein.

## Übersetzungen

OpenSense verwendet die Anzeigesprache von Windows oder die unter **Einstellungen → Darstellung → Sprache** gewählte. Noch hat kein Muttersprachler die Übersetzungen geprüft, auch diese Seite nicht, daher sind Korrekturen willkommen.

Die Texte der App liegen in `src/OpenSense.App/Strings/<language>/Resources.resw`, die des Installationsprogramms in `installer/Strings/<language>.nsh`, mit einer Anmerkung zu jedem Text in den englischen Dateien; `dotnet test` prüft, ob jede Übersetzung alle Texte und Platzhalter enthält. Die Übersetzungen dieser Seite liegen in [`docs`](.).

## Erstellen

Du brauchst das [.NET 10 SDK](https://dotnet.microsoft.com/download) unter Windows.

```powershell
dotnet build
dotnet test --project tests/OpenSense.Core.Tests
```

Jeder Push wird von [GitHub Actions](../.github/workflows/build.yml) erstellt, getestet und verpackt; dort sieht man auch, wie das Installationsprogramm mit [NSIS](https://nsis.sourceforge.io) entsteht. Ein gepushtes Tag wie `v1.2.3` veröffentlicht ein Release.

## Danksagungen

- [PawnIO](https://pawnio.eu): der signierte Treiber, der die CPU-Temperaturen liest (seine Module stehen unter LGPL-2.1)
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK), [Win2D](https://github.com/microsoft/Win2D) und das [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon), [StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc) und [Serilog](https://serilog.net)

## Lizenz

OpenSense steht unter der [GNU General Public License v3.0 oder neuer](../LICENSE).

Es ist ein unabhängiges Projekt, auf Grundlage einer Interoperabilitätsanalyse geschrieben, und enthält keinen Code von Acer. Es ist weder mit Acer verbunden noch von Acer unterstützt. Acer, Nitro, Predator und NitroSense sind Marken der Acer Inc.
