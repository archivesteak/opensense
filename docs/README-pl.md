<div align="center">

![OpenSense](../resources/banner.png)

**Sterowanie wentylatorami, wydajnością i podświetleniem w laptopach Acer Nitro i Predator.**<br>
Otwartoźródłowy zamiennik NitroSense i PredatorSense.

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&logo=gnu&color=C4282D)](../LICENSE)

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
- **Sterowanie wentylatorami.** Auto, które OpenSense przyspiesza, gdy laptop się nagrzewa (można to wyłączyć), Maks. albo Ręczny: dodatkowa prędkość dla każdego wentylatora, stała lub według krzywej, którą rysujesz sam. W laptopach, które je mają, krzywą wentylatorów samego oprogramowania układowego można przyspieszyć, a DustDefender wydmuchuje kurz.
- **Bezpieczne domyślnie.** Ochrona przed dławieniem rozkręca wentylatory do pełnej prędkości, gdy procesor zbliża się do progu dławienia, a gdy czujnik lub OpenSense przestanie działać, sterowanie ponownie przejmuje oprogramowanie układowe.
- **Wydajność.** Tryby pracy, CoolBoost, podkręcanie GPU dla każdego trybu i plany zasilania systemu Windows. Klawisz trybu przełącza tryby, a na baterii działa osobny tryb. W Predatorach z 2024 roku i nowszych dochodzi do tego podkręcanie GPU ustawione przez Acera dla każdego trybu.
- **Podświetlenie.** Stałe kolory dla każdej strefy klawiatury albo efekty Oddychanie, Neon, Fala, Przesuwanie, Powiększanie, Meteor i Migotanie. Także listwy świetlne, Infinity Mirror, InfiniteRing, logo oraz klawisze Turbo i trybu, każde z własnymi efektami. Klawiatury z podświetleniem każdego klawisza osobno i klawisze MagForce przyjmują kolor dla każdego klawisza i mają własne efekty.
- **Klawiatura i ekran.** Automatyczne wyłączanie podświetlenia, blokada klawisza Windows, Overdrive LCD i przełącznik GPU (MUX).
- **Bateria.** Kondycja baterii (pozostała pojemność i cykle ładowania), zatrzymywanie ładowania na 80%, kalibracja baterii i ładowanie urządzeń USB po wyłączeniu laptopa.
- **Uruchamianie.** Animacja i dźwięk uruchamiania oraz własne logo uruchamiania w laptopach, które to obsługują.
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

### Zrzuty oprogramowania układowego

Czegoś brakuje albo coś nie działa dobrze na twoim laptopie? Wyślij kopię jego oprogramowania układowego, a zostanie przeanalizowana, żeby ustalić, jak to działa w twoim modelu. Tak rozpracowano wentylatory AN515-57: jego oprogramowanie układowe pokazało, że ich prędkość można zwiększać tylko co 10%, a wbudowana krzywa wentylatorów nic nie zmienia. [Ten poradnik](firmware/dump-firmware-pl.md) wyjaśnia, jak zrobić kopię z pendrive’a z Linuksem, niczego nie zmieniając w laptopie.

Kopia pochodzi wyłącznie z układu, w którym zapisane jest oprogramowanie laptopa: nie ma w niej żadnych twoich plików, kont ani niczego innego z systemu Windows. Jedyne dane osobowe, jakie zawiera, to numer seryjny laptopa i klucz licencyjny systemu Windows zapisany przez Acera w oprogramowaniu układowym. Jeśli wolisz ich nie publikować, w poradniku znajdziesz sposób, żeby wysłać kopię prywatnie.

Najbardziej pomogłyby te modele:

- **Nitro AN515-46, AN515-47, AN515-58, AN517-42, AN517-43 i AN517-55**: jedyne modele, w których oprogramowanie Acera ustawia opcję **Krzywa wentylatorów**, więc jedyne, w których OpenSense ją pokazuje. Nikt jeszcze nie sprawdził, co w nich zmienia.
- **Predator Helios 16 i 18 z lat 2024 i 2025 (PH16-72, PH18-72, PH16-73, PH18-73) oraz Helios Neo 16 (PHN16-72)**: w Predatorach z 2024 roku i nowszych tryby pracy i fabryczne podkręcanie GPU od Acera działają przez interfejs HID kontrolera wbudowanego, którym OpenSense steruje wyłącznie na podstawie oprogramowania Acera.
- **Predator Helios 16 i 18 z 2023 roku (PH16-71, PH18-71) oraz Helios 3D 15 (PH3D15-71)**: tylna listwa świetlna, której efekty generuje kontroler wbudowany.
- **Każdy inny model**: OpenSense na każdym laptopie zwiększa prędkość wentylatorów co 10%, bo kontroler AN515-57 pomija wartości pośrednie. Zrzut pokaże, czy twój robi tak samo.

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
