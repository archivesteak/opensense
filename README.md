<div align="center">

![OpenSense](resources/banner.png)

**Fan, performance and lighting control for Acer Nitro and Predator laptops.**<br>
An open-source replacement for NitroSense.

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&color=E4473C)](LICENSE)

[**Download**](https://github.com/archivesteak/opensense/releases/latest) •
[Features](#features) •
[Supported laptops](#supported-laptops) •
[Building](#building)

**English** |
[Bahasa Indonesia](docs/README-id.md) |
[Deutsch](docs/README-de.md) |
[Español](docs/README-es.md) |
[Français](docs/README-fr.md) |
[Polski](docs/README-pl.md) |
[Português (Brasil)](docs/README-pt-BR.md) |
[Tiếng Việt](docs/README-vi.md) |
[Türkçe](docs/README-tr.md) |
[Русский](docs/README-ru.md) |
[Українська](docs/README-uk.md) |
[简体中文](docs/README-zh-CN.md) |
[繁體中文](docs/README-zh-TW.md)

Help translate OpenSense and this page into your language: see [Translations](#translations).

</div>

## Features

- **Real temperatures.** CPU and GPU are read from the chips themselves, like HWiNFO and ThrottleStop do, with 5-minute graphs.
- **Fan control.** Auto, Max, a fixed speed per fan, or temperature curves you draw yourself.
- **Safe by default.** Fans go to full speed at an emergency temperature, and the firmware takes over again if a sensor or OpenSense stops.
- **Performance.** Operating modes, CoolBoost and Windows power plans.
- **Lighting.** Static colours per keyboard zone, or Breathing, Neon, Wave, Shifting and Zoom effects.
- **Keyboard and display.** Backlight auto-off, Windows-key lock, LCD overdrive and the GPU (MUX) switch.
- **No admin prompts.** A small background service applies your settings from startup, and the NitroSense key opens the app.
- **Your language.** 36 languages, following Windows or chosen in Settings.

OpenSense asks the firmware what your laptop has and shows only what it supports.

## Installation

Download the latest version from [**Releases**](https://github.com/archivesteak/opensense/releases/latest) (Windows 10 2004 or later, 64-bit):

- **`OpenSense-<version>-Setup-x64.exe`**: the installer. It includes everything OpenSense needs and keeps itself up to date.
- **`OpenSense-<version>-Portable-x64.zip`**: no installation. It asks for administrator rights and controls the laptop only while it's open.
  It doesn't include the PawnIO driver, so until you install it separately from [PawnIO's releases](https://github.com/namazso/PawnIO.Setup/releases/latest), the CPU temperature comes from the laptop's firmware, which is less exact.

> [!NOTE]
> Turn off NitroSense (or uninstall it) before using OpenSense, otherwise the two will fight over the fans.

## Supported laptops

Acer laptops with the gaming firmware interface that NitroSense and PredatorSense use. OpenSense is developed on a **Nitro 5 AN515-57**.

Tried it on another model? [Open an issue](https://github.com/archivesteak/opensense/issues) and paste the diagnostics from **Settings → Troubleshooting → Copy**.

## Translations

OpenSense uses the Windows display language, or the one picked in **Settings → Appearance → Language**. No native speaker has checked the translations yet, so corrections are welcome.

The app's text is in `src/OpenSense.App/Strings/<language>/Resources.resw` and the installer's in `installer/Strings/<language>.nsh`, with a note on each string in the English files; `dotnet test` checks that every translation has all the strings and placeholders. This page's translations are in [`docs`](docs).

## Building

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
dotnet build
dotnet test --project tests/OpenSense.Core.Tests
```

Every push is built, tested and packaged by [GitHub Actions](.github/workflows/build.yml), which also shows how the installer is made with [NSIS](https://nsis.sourceforge.io). Pushing a `v1.2.3` tag publishes a release.

## Credits

- [PawnIO](https://pawnio.eu): the signed driver that reads CPU temperatures (its modules are LGPL-2.1)
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK), [Win2D](https://github.com/microsoft/Win2D) and the [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon), [StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc) and [Serilog](https://serilog.net)

## License

OpenSense is licensed under the [GNU General Public License v3.0 or later](LICENSE).

It is an independent project, written from interoperability analysis, and contains no Acer code. It is not affiliated with or endorsed by Acer. Acer, Nitro, Predator and NitroSense are trademarks of Acer Inc.
