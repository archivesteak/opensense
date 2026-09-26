# Die Firmware deines Laptops auslesen

[English](dump-firmware.md) |
[Bahasa Indonesia](dump-firmware-id.md) |
**Deutsch** |
[Español](dump-firmware-es.md) |
[Français](dump-firmware-fr.md) |
[Polski](dump-firmware-pl.md) |
[Português (Brasil)](dump-firmware-pt-BR.md) |
[Tiếng Việt](dump-firmware-vi.md) |
[Türkçe](dump-firmware-tr.md) |
[Русский](dump-firmware-ru.md) |
[Українська](dump-firmware-uk.md) |
[简体中文](dump-firmware-zh-CN.md) |
[繁體中文](dump-firmware-zh-TW.md)

Fehlt auf deinem Laptop eine Funktion oder funktioniert sie nicht richtig, zeigt eine Kopie seiner Firmware, wie dein Modell das tatsächlich umsetzt. Vieles, was OpenSense über die Lüfter weiß, stammt aus der Firmware des Nitro 5 AN515-57: Daher ist bekannt, dass die Lüfter nur in Zehnerschritten beschleunigt werden und dass die Lüfterkurve der Firmware auf diesem Modell nichts bewirkt. Welche Modelle am meisten helfen würden, steht in der [README](../README-de.md#firmware-dumps).

Mit dieser Anleitung liest du den BIOS-Flash-Chip des Laptops von einem Linux-Live-USB-Stick aus und speicherst seinen Inhalt in einer Datei. Beim AN515-57 steckte in dieser Datei auch die Firmware des Embedded Controllers (EC), der die Lüfter und die Tastaturbeleuchtung steuert. **Auf den Laptop wird nichts geschrieben.** Das Ganze dauert etwa eine halbe Stunde.

## Was du brauchst

- Einen USB-Stick mit mindestens 4 GB für Linux (er wird gelöscht).
- Einen Ort zum Speichern des Dumps: einen zweiten USB-Stick oder eine Partition des Laptops, die nicht das Windows-Laufwerk ist, etwa ein Datenlaufwerk `D:`.
- Das Netzteil: Der Laptop sollte die ganze Zeit angeschlossen bleiben.

> [!IMPORTANT]
> **BitLocker.** Viele Laptops werden mit verschlüsseltem Windows-Laufwerk ausgeliefert (*Geräteverschlüsselung*). Wenn du Secure Boot ausschaltest, fragt Windows beim nächsten Start nach dem BitLocker-Wiederherstellungsschlüssel. Suche vorher deinen Schlüssel heraus (er ist in deinem Microsoft-Konto gespeichert: [aka.ms/myrecoverykey](https://aka.ms/myrecoverykey)) oder setze BitLocker aus, bis du es wieder einschaltest. Gib dazu in einem als Administrator ausgeführten Terminal ein:
>
> ```powershell
> manage-bde -protectors -disable C: -RebootCount 0
> ```

## 1. Den Linux-USB-Stick erstellen

Lade **Linux Mint** (die Cinnamon-Edition) von [linuxmint.com](https://linuxmint.com/download.php) herunter und schreibe es mit [Rufus](https://rufus.ie) auf den USB-Stick. An den Standardeinstellungen musst du nichts ändern.

## 2. Secure Boot ausschalten

Solange Secure Boot eingeschaltet ist, lässt Linux keinen direkten Zugriff auf die Hardware zu, und ohne den lässt sich der Chip nicht auslesen. Die Punkte unten heißen so wie im deutschen BIOS-Setup; ist deines auf Englisch, halte dich an die Namen in Klammern.

1. Starte den Laptop neu und drücke wiederholt **F2**, während das Acer-Logo zu sehen ist, um das BIOS-Setup zu öffnen.
2. Bei Acer lässt sich **Sicheres Booten** (Secure Boot) erst ändern, wenn ein Supervisor-Kennwort gesetzt ist. Wähle auf der Registerkarte **Sicherheit** (Security) den Punkt **Supervisor-Kennwort einstellen** (Set Supervisor Password) und lege ein Kennwort fest.
3. Stelle auf der Registerkarte **Boot** den Punkt **Sicheres Booten** (Secure Boot) auf **Deaktiviert** (Disabled).
4. Stelle auf der Registerkarte **Hauptmenü** (Main) das **F12 Boot-Menü** (F12 Boot Menu) auf **Aktiviert** (Enabled), falls es noch nicht aktiviert ist.
5. Drücke **F10**, um zu speichern und neu zu starten.

## 3. Linux vom USB-Stick starten

Stecke den Stick ein, starte neu und drücke **F12**, während das Acer-Logo zu sehen ist. Wähle den USB-Stick und dann **Start Linux Mint**. Linux läuft direkt vom Stick und lässt Windows unberührt.

## 4. flashrom installieren

Verbinde dich mit dem WLAN oder per Kabel (Netzwerksymbol unten rechts), öffne **Terminal** und gib ein:

```sh
sudo apt update
sudo apt install -y flashrom
```

## 5. Den Chip auslesen

Lass flashrom zuerst den Chip suchen. Dieser Befehl liest noch nichts aus:

```sh
sudo flashrom -p internal
```

Er zeigt Namen und Größe des Chips an und bricht dann mit der Warnung ab, dass es sich um einen Laptop handelt. Das ist so gewollt: Auf Laptops verweigert flashrom standardmäßig den Dienst, damit nicht versehentlich etwas geschrieben wird, und hier wird nur gelesen. Findet flashrom mehrere passende Chips, ergänze die Befehle unten um `-c "NAME"` mit einem dieser Namen.

Lies den Chip jetzt zweimal aus und vergleiche die beiden Kopien:

```sh
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios1.bin 2>&1 | tee ~/flashrom.txt
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios2.bin
cmp ~/bios1.bin ~/bios2.bin && echo "DUMP OK" || echo "DIFFERENT, read again"
```

Es sollte `DUMP OK` erscheinen. Unterscheiden sich die Kopien, wiederhole die beiden Lesebefehle. Große Bereiche, die nur aus `FF` bestehen (der Intel-ME-Bereich), sind normal.

Kann flashrom den Chip gar nicht lesen (das kommt bei manchen AMD-Laptops vor), speichere `flashrom.txt`: Auch die Fehlermeldung darin hilft weiter.

## 6. Den Dump speichern

Das Live-System hält seine Dateien im Arbeitsspeicher, beim Herunterfahren sind sie also weg. Öffne die App **Files** und klicke in der Seitenleiste auf deinen zweiten USB-Stick oder dein Datenlaufwerk, damit es eingehängt wird. Sieh dann nach, wo es eingehängt wurde:

```sh
lsblk -o NAME,SIZE,FSTYPE,LABEL,MOUNTPOINTS
```

Kopiere die Dateien in diesen Ordner (der Pfad in der Spalte `MOUNTPOINTS`, zum Beispiel `/media/mint/Data`):

```sh
cp ~/bios1.bin ~/flashrom.txt /media/mint/Data/
sync
```

## 7. Alles zurückstellen

1. Starte neu, ziehe den USB-Stick ab und drücke **F2**, um das BIOS-Setup wieder zu öffnen.
2. Stelle auf der Registerkarte **Boot** den Punkt **Sicheres Booten** (Secure Boot) wieder auf **Aktiviert** (Enabled).
3. Wähle auf der Registerkarte **Sicherheit** (Security) den Punkt **Supervisor-Kennwort einstellen** (Set Supervisor Password), gib das aktuelle Kennwort ein und lass das Feld für das neue leer: So wird das Kennwort entfernt.
4. Drücke **F10**, um zu speichern und Windows zu starten.

Hast du BitLocker ausgesetzt, schalte es in einem als Administrator ausgeführten Terminal wieder ein:

```powershell
manage-bde -protectors -enable C:
```

## 8. Abschicken

Gebraucht wird nur `bios1.bin` (und `flashrom.txt`, falls das Auslesen fehlgeschlagen ist). [Erstelle ein Issue](https://github.com/archivesteak/opensense/issues), gib Modell und BIOS-Version deines Laptops an (OpenSense zeigt beides unter **Einstellungen → Ihr Laptop**) und hänge den Dump an. GitHub nimmt Dateien bis 25 MB und nur bestimmte Typen an: Ist der Dump größer oder lehnt GitHub ihn ab, packe ihn vorher in ein `.zip`-Archiv. Ist das Archiv immer noch zu groß, lade es bei einem beliebigen Filehoster hoch und füge den Link ein.

> [!WARNING]
> Issues sind öffentlich, und der Dump enthält Angaben zu deinem Laptop: seine Seriennummer, den Windows-Lizenzschlüssel, den Acer in der Firmware hinterlegt hat, und das Supervisor-Kennwort, das du in Schritt 2 gesetzt hast (deshalb sollte es eines sein, das du sonst nirgends verwendest). Möchtest du sie nicht veröffentlichen, schicke das ZIP (oder einen Link dazu) stattdessen per E-Mail an [archivesteak@gmail.com](mailto:archivesteak@gmail.com), zusammen mit Modell und BIOS-Version deines Laptops.
