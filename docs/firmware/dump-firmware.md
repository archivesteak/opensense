# Dumping your laptop's firmware

**English** |
[Bahasa Indonesia](dump-firmware-id.md) |
[Deutsch](dump-firmware-de.md) |
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

If a feature is missing or doesn't work right on your laptop, a copy of its firmware shows how your model really does
it. Much of what OpenSense knows about the fans comes from reading the Nitro 5 AN515-57's own firmware: that's how it
found that fan boosts go out in steps of 10%, and that the firmware's fan curve does nothing on that model. The models
that would help most are listed in the [README](../../README.md#firmware-dumps).

This guide reads the laptop's BIOS flash chip from a Linux live USB stick and saves it as one file. On the AN515-57
that one file also held the embedded controller's firmware, which is the part that runs the fans and the keyboard
lights. **Nothing is written to the laptop.** It takes about half an hour.

## What you need

- A USB stick of 4 GB or more for Linux (it will be erased).
- Somewhere to save the dump: a second USB stick, or a partition on the laptop that isn't the Windows drive, such
  as a `D:` data drive.
- The laptop plugged in, for the whole time.

> [!IMPORTANT]
> **BitLocker.** Many laptops come with their Windows drive encrypted (*Device encryption*). Turning Secure Boot off
> makes Windows ask for the BitLocker recovery key at the next start. Before you begin, either find your key (it's
> in your Microsoft account at [aka.ms/myrecoverykey](https://aka.ms/myrecoverykey)) or suspend BitLocker until you
> turn it back on, in a terminal run as administrator:
>
> ```powershell
> manage-bde -protectors -disable C: -RebootCount 0
> ```

## 1. Make the Linux USB stick

Download **Linux Mint** (the Cinnamon edition) from [linuxmint.com](https://linuxmint.com/download.php) and write it
to the USB stick with [Rufus](https://rufus.ie). Rufus's default settings are fine.

## 2. Turn Secure Boot off

While Secure Boot is on, Linux blocks the direct hardware access that reading the chip needs.

1. Restart the laptop and press **F2** repeatedly while the Acer logo shows, to open the BIOS setup.
2. Acer only lets you change Secure Boot once there is a supervisor password. On the **Security** tab, choose
   **Set Supervisor Password** and set one.
3. On the **Boot** tab, set **Secure Boot** to **Disabled**.
4. On the **Main** tab, set **F12 Boot Menu** to **Enabled** if it isn't already.
5. Press **F10** to save and restart.

## 3. Start Linux from the USB stick

Plug the stick in, restart, and press **F12** while the Acer logo shows. Choose the USB stick, then
**Start Linux Mint**. Linux runs from the stick and doesn't touch Windows.

## 4. Install flashrom

Connect to Wi-Fi or Ethernet (the network icon, bottom right), open **Terminal** and run:

```sh
sudo apt update
sudo apt install -y flashrom
```

## 5. Read the chip

First let flashrom find the chip. This reads nothing yet:

```sh
sudo flashrom -p internal
```

It prints the chip's name and size, then stops with a warning that this is a laptop. That's expected: flashrom
refuses laptops to protect them from writes, and this guide only reads. If it lists several possible chips, add
`-c "NAME"` with one of their names to the commands below.

Now read the chip twice and compare the two copies:

```sh
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios1.bin 2>&1 | tee ~/flashrom.txt
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios2.bin
cmp ~/bios1.bin ~/bios2.bin && echo "DUMP OK" || echo "DIFFERENT, read again"
```

You want `DUMP OK`. If the copies differ, run the two reads again. Parts of the file that read as `FF` all the way
(the Intel ME region) are normal.

If flashrom can't read the chip at all (this can happen on some AMD laptops), keep `flashrom.txt`: its error message
is useful too.

## 6. Save the dump

The live system keeps its files in memory, so they're gone when it shuts down. Open the **Files** app and click your second
USB stick or your data drive in the sidebar, which mounts it. Then find where it's mounted:

```sh
lsblk -o NAME,SIZE,FSTYPE,LABEL,MOUNTPOINTS
```

Copy the files to that folder (the path in the `MOUNTPOINTS` column, for example `/media/mint/Data`):

```sh
cp ~/bios1.bin ~/flashrom.txt /media/mint/Data/
sync
```

## 7. Put everything back

1. Restart, remove the USB stick and press **F2** to open the BIOS setup again.
2. On the **Boot** tab, set **Secure Boot** back to **Enabled**.
3. On the **Security** tab, choose **Set Supervisor Password**, enter the current one and leave the new one empty to
   remove it.
4. Press **F10** to save and restart into Windows.

If you suspended BitLocker, turn it back on in a terminal run as administrator:

```powershell
manage-bde -protectors -enable C:
```

## 8. Send it

Only `bios1.bin` matters (with `flashrom.txt` if the read failed).
[Open an issue](https://github.com/archivesteak/opensense/issues), write your laptop's model and BIOS version (OpenSense
shows both under **Settings → Your laptop**) and attach the dump. GitHub takes files up to 25 MB, and only some
types: if the dump is bigger or GitHub refuses it, compress it into a `.zip` archive first. If the archive is
still too big, upload it somewhere else and paste the link.

> [!WARNING]
> Issues are public, and the dump contains details about your laptop: its serial number, the Windows licence key
> Acer stored in the firmware, and the supervisor password you set in step 2 (which is why it should be one you don't
> use elsewhere). If you'd rather not publish them, email the zip (or a link to it) to
> [archivesteak@gmail.com](mailto:archivesteak@gmail.com) instead, with your laptop's model and BIOS version.
