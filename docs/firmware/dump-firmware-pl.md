# Zrzut oprogramowania układowego laptopa

[English](dump-firmware.md) |
[Bahasa Indonesia](dump-firmware-id.md) |
[Deutsch](dump-firmware-de.md) |
[Español](dump-firmware-es.md) |
[Français](dump-firmware-fr.md) |
**Polski** |
[Português (Brasil)](dump-firmware-pt-BR.md) |
[Tiếng Việt](dump-firmware-vi.md) |
[Türkçe](dump-firmware-tr.md) |
[Русский](dump-firmware-ru.md) |
[Українська](dump-firmware-uk.md) |
[简体中文](dump-firmware-zh-CN.md) |
[繁體中文](dump-firmware-zh-TW.md)

Jeśli na twoim laptopie brakuje jakiejś funkcji albo działa ona nieprawidłowo, kopia jego oprogramowania układowego pokaże, jak to naprawdę działa w twoim modelu. Wiele z tego, co OpenSense wie o wentylatorach, pochodzi z oprogramowania układowego Nitro 5 AN515-57: tak ustalono, że prędkość wentylatorów można zwiększać tylko co 10% i że wbudowana w oprogramowanie krzywa wentylatorów nic w tym modelu nie zmienia. Lista modeli, których zrzuty najbardziej by pomogły, jest w [README](../README-pl.md#zrzuty-oprogramowania-układowego).

Dzięki temu poradnikowi odczytasz układ flash BIOS-u laptopa z pendrive’a z Linuksem w trybie live i zapiszesz jego zawartość w jednym pliku. W AN515-57 w tym samym pliku było też oprogramowanie kontrolera wbudowanego (EC), który steruje wentylatorami i podświetleniem klawiatury. **Nic nie jest zapisywane w laptopie.** Zajmuje to około pół godziny.

## Czego potrzebujesz

- Pendrive’a o pojemności co najmniej 4 GB na Linuksa (zostanie wyczyszczony).
- Miejsca na zapisanie zrzutu: drugiego pendrive’a albo partycji laptopa, która nie jest dyskiem Windows, np. dysku z danymi `D:`.
- Zasilacza: laptop powinien być cały czas podłączony do prądu.

> [!IMPORTANT]
> **BitLocker.** Wiele laptopów ma dysk Windows zaszyfrowany fabrycznie (*Szyfrowanie urządzenia*). Po wyłączeniu Secure Boot Windows przy następnym uruchomieniu poprosi o klucz odzyskiwania funkcji BitLocker. Zanim zaczniesz, znajdź swój klucz (jest na twoim koncie Microsoft pod adresem [aka.ms/myrecoverykey](https://aka.ms/myrecoverykey)) albo wstrzymaj funkcję BitLocker, dopóki nie włączysz jej ponownie. W tym celu wpisz w terminalu uruchomionym jako administrator:
>
> ```powershell
> manage-bde -protectors -disable C: -RebootCount 0
> ```

## 1. Przygotuj pendrive’a z Linuksem

Pobierz **Linux Mint** (wydanie Cinnamon) z [linuxmint.com](https://linuxmint.com/download.php) i nagraj obraz na pendrive’a za pomocą programu [Rufus](https://rufus.ie). Domyślnych ustawień nie trzeba zmieniać.

## 2. Wyłącz Secure Boot

Gdy Secure Boot jest włączony, Linux nie pozwala programom na bezpośredni dostęp do sprzętu, a bez niego nie da się odczytać układu. BIOS nie ma polskiej wersji, więc opcje poniżej są podane po angielsku, tak jak na ekranie.

1. Uruchom laptopa ponownie i, gdy na ekranie jest logo Acera, kilka razy naciśnij **F2**, żeby wejść do ustawień BIOS-u.
2. Acer pozwala zmienić **Secure Boot** dopiero po ustawieniu hasła supervisora. Na karcie **Security** wybierz **Set Supervisor Password** i ustaw hasło.
3. Na karcie **Boot** ustaw **Secure Boot** na **Disabled**.
4. Na karcie **Main** ustaw **F12 Boot Menu** na **Enabled**, jeśli ta opcja nie jest jeszcze włączona.
5. Naciśnij **F10**, żeby zapisać zmiany i uruchomić laptopa ponownie.

## 3. Uruchom Linuksa z pendrive’a

Podłącz pendrive’a, uruchom laptopa ponownie i naciśnij **F12**, gdy na ekranie jest logo Acera. Wybierz pendrive’a, a potem **Start Linux Mint**. Linux uruchomi się prosto z pendrive’a i nie naruszy Windowsa.

## 4. Zainstaluj flashrom

Połącz się z Wi-Fi albo przez kabel (ikona sieci w prawym dolnym rogu), otwórz **Terminal** i wpisz:

```sh
sudo apt update
sudo apt install -y flashrom
```

## 5. Odczytaj układ

Najpierw niech flashrom wykryje układ. To polecenie jeszcze niczego nie odczytuje:

```sh
sudo flashrom -p internal
```

Wyświetli nazwę i rozmiar układu, a potem zatrzyma się z ostrzeżeniem, że to laptop. Tak ma być: na laptopach flashrom domyślnie odmawia działania, żeby przypadkiem niczego nie zapisać, a tutaj tylko odczytujemy. Jeśli znajdzie kilka pasujących układów, dodaj do poniższych poleceń `-c "NAZWA"` z jedną z tych nazw.

Teraz odczytaj układ dwa razy i porównaj obie kopie:

```sh
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios1.bin 2>&1 | tee ~/flashrom.txt
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios2.bin
cmp ~/bios1.bin ~/bios2.bin && echo "DUMP OK" || echo "DIFFERENT, read again"
```

Powinien pojawić się komunikat `DUMP OK`. Jeśli kopie się różnią, powtórz oba odczyty. Duże fragmenty wypełnione samymi `FF` (obszar Intel ME) są normalne.

Jeśli flashrom w ogóle nie może odczytać układu (może się to zdarzyć w niektórych laptopach z AMD), zachowaj plik `flashrom.txt`: komunikat o błędzie, który zawiera, też się przyda.

## 6. Zapisz zrzut

System live trzyma pliki w pamięci RAM, więc po wyłączeniu laptopa znikną. Otwórz aplikację **Files** i kliknij na pasku bocznym drugi pendrive albo dysk z danymi, żeby go zamontować. Potem sprawdź, gdzie został zamontowany:

```sh
lsblk -o NAME,SIZE,FSTYPE,LABEL,MOUNTPOINTS
```

Skopiuj pliki do tego folderu (ścieżka z kolumny `MOUNTPOINTS`, np. `/media/mint/Data`):

```sh
cp ~/bios1.bin ~/flashrom.txt /media/mint/Data/
sync
```

## 7. Przywróć poprzednie ustawienia

1. Uruchom ponownie, wyjmij pendrive’a i naciśnij **F2**, żeby znowu otworzyć ustawienia BIOS-u.
2. Na karcie **Boot** ustaw **Secure Boot** z powrotem na **Enabled**.
3. Na karcie **Security** wybierz **Set Supervisor Password**, wpisz obecne hasło, a pole nowego zostaw puste: w ten sposób hasło zostanie usunięte.
4. Naciśnij **F10**, żeby zapisać zmiany i uruchomić Windowsa.

Jeśli funkcja BitLocker była wstrzymana, włącz ją ponownie. Wpisz w terminalu uruchomionym jako administrator:

```powershell
manage-bde -protectors -enable C:
```

## 8. Wyślij zrzut

Potrzebny jest tylko `bios1.bin` (i `flashrom.txt`, jeśli odczyt się nie udał). [Otwórz issue](https://github.com/archivesteak/opensense/issues), podaj model i wersję BIOS-u laptopa (OpenSense pokazuje je w **Ustawienia → Twój laptop**) i dołącz zrzut. GitHub przyjmuje pliki do 25 MB i tylko niektórych typów: jeśli zrzut jest większy albo GitHub go odrzuca, najpierw skompresuj go do archiwum `.zip`. Jeśli archiwum nadal jest za duże, prześlij je do dowolnego serwisu z plikami i wklej link.

> [!WARNING]
> Zgłoszenia na GitHubie są publiczne, a zrzut zawiera dane o twoim laptopie: jego numer seryjny, klucz licencyjny systemu Windows zapisany przez Acera w oprogramowaniu układowym i hasło supervisora ustawione w kroku 2 (dlatego powinno to być hasło, którego nie używasz nigdzie indziej). Jeśli wolisz ich nie publikować, wyślij zip (albo link do niego) e-mailem na adres [archivesteak@gmail.com](mailto:archivesteak@gmail.com), razem z modelem i wersją BIOS-u laptopa.
