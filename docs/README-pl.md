<div align="center">

![OpenSense](../resources/banner.png)

**Sterowanie wentylatorami, wydajnością i podświetleniem w laptopach Acer Nitro i Predator.**<br>
Otwartoźródłowy zamiennik NitroSense.

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&color=E4473C)](../LICENSE)

[**Pobierz**](https://github.com/archivesteak/opensense/releases/latest) •
[Funkcje](#funkcje) •
[Obsługiwane laptopy](#obsługiwane-laptopy) •
[Kompilacja](#kompilacja)

[English](../README.md) |
[Bahasa Indonesia](README-id.md) |
[Deutsch](README-de.md) |
[Español](README-es.md) |
[Français](README-fr.md) |
**Polski** |
[Português (Brasil)](README-pt-BR.md) |
[Tiếng Việt](README-vi.md) |
[Türkçe](README-tr.md) |
[Русский](README-ru.md) |
[Українська](README-uk.md) |
[简体中文](README-zh-CN.md) |
[繁體中文](README-zh-TW.md)

Pomóż przetłumaczyć OpenSense i tę stronę na swój język: zobacz [Tłumaczenia](#tłumaczenia).

</div>

## Funkcje

- **Prawdziwe temperatury.** Temperatury CPU i GPU są odczytywane z samych układów, tak jak robią to HWiNFO i ThrottleStop, z wykresami z 5 minut.
- **Sterowanie wentylatorami.** Auto, które OpenSense przyspiesza, gdy laptop się nagrzewa (można to wyłączyć), Maks. albo Ręczny: dodatkowa prędkość dla każdego wentylatora, stała lub według krzywej, którą rysujesz sam.
- **Bezpieczne domyślnie.** Ochrona przed dławieniem rozkręca wentylatory do pełnej prędkości, gdy procesor zbliża się do progu dławienia, a gdy czujnik lub OpenSense przestanie działać, sterowanie ponownie przejmuje oprogramowanie układowe.
- **Wydajność.** Tryby pracy, CoolBoost i plany zasilania systemu Windows.
- **Podświetlenie.** Stałe kolory dla każdej strefy klawiatury albo efekty Oddychanie, Neon, Fala, Przesuwanie i Powiększanie.
- **Klawiatura i ekran.** Automatyczne wyłączanie podświetlenia, blokada klawisza Windows, Overdrive LCD i przełącznik GPU (MUX).
- **Bez monitów administratora.** Niewielka usługa w tle stosuje Twoje ustawienia od momentu uruchomienia, a klawisz NitroSense otwiera aplikację.
- **Twój język.** 36 języków, zgodnie z systemem Windows lub według wyboru w ustawieniach.

OpenSense pyta oprogramowanie układowe, co ma Twój laptop, i pokazuje tylko to, co jest obsługiwane.

## Instalacja

Pobierz najnowszą wersję z [**Releases**](https://github.com/archivesteak/opensense/releases/latest) (Windows 10 2004 lub nowszy, 64-bitowy):

- **`OpenSense-<version>-Setup-x64.exe`**: instalator. Zawiera wszystko, czego potrzebuje OpenSense, i sam dba o aktualizacje.
- **`OpenSense-<version>-Portable-x64.zip`**: bez instalacji. Prosi o uprawnienia administratora i steruje laptopem tylko wtedy, gdy jest otwarty.
  Nie zawiera sterownika PawnIO, więc dopóki nie zainstalujesz go osobno ze [strony wydań PawnIO](https://github.com/namazso/PawnIO.Setup/releases/latest), temperatura CPU pochodzi z oprogramowania układowego laptopa i jest mniej dokładna.

> [!NOTE]
> Przed użyciem OpenSense wyłącz NitroSense (lub go odinstaluj), inaczej oba programy będą walczyć o wentylatory.

## Obsługiwane laptopy

Laptopy Acer z interfejsem oprogramowania układowego dla graczy, z którego korzystają NitroSense i PredatorSense.

OpenSense powstał na **Nitro 5 AN515-57**.

Wypróbowałeś na innym modelu? [Otwórz issue](https://github.com/archivesteak/opensense/issues) i wklej dane diagnostyczne z **Ustawienia → Rozwiązywanie problemów → Kopiuj**.

## Tłumaczenia

OpenSense używa języka wyświetlania systemu Windows albo języka wybranego w **Ustawienia → Wygląd → Język**. Żaden rodzimy użytkownik języka nie sprawdził jeszcze tłumaczeń, w tym tej strony, więc poprawki są mile widziane.

Teksty aplikacji znajdują się w `src/OpenSense.App/Strings/<language>/Resources.resw`, a teksty instalatora w `installer/Strings/<language>.nsh`, z uwagą do każdego tekstu w plikach angielskich; `dotnet test` sprawdza, czy każde tłumaczenie ma wszystkie teksty i symbole zastępcze. Tłumaczenia tej strony znajdują się w [`docs`](.).

## Kompilacja

Potrzebujesz [.NET 10 SDK](https://dotnet.microsoft.com/download) w systemie Windows.

```powershell
dotnet build
dotnet test --project tests/OpenSense.Core.Tests
```

Każdy push jest kompilowany, testowany i pakowany przez [GitHub Actions](../.github/workflows/build.yml); tam też widać, jak powstaje instalator w [NSIS](https://nsis.sourceforge.io). Wypchnięcie tagu `v1.2.3` publikuje wydanie.

## Podziękowania

- [PawnIO](https://pawnio.eu): podpisany sterownik, który odczytuje temperatury CPU (jego moduły są na licencji LGPL-2.1)
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK), [Win2D](https://github.com/microsoft/Win2D) i [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon), [StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc) i [Serilog](https://serilog.net)

## Licencja

OpenSense jest udostępniany na licencji [GNU General Public License v3.0 lub nowszej](../LICENSE).

To niezależny projekt, napisany na podstawie analizy interoperacyjności, i nie zawiera kodu Acer. Nie jest powiązany z firmą Acer ani przez nią zatwierdzony. Acer, Nitro, Predator i NitroSense są znakami towarowymi Acer Inc.
