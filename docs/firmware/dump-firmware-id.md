# Membuat dump firmware laptop Anda

[English](dump-firmware.md) |
**Bahasa Indonesia** |
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

Jika ada fitur yang tidak tersedia atau tidak berjalan dengan benar di laptop Anda, salinan firmware-nya menunjukkan cara kerjanya yang sebenarnya pada model Anda. Sebagian besar yang diketahui OpenSense tentang kipas berasal dari firmware Nitro 5 AN515-57: dari situlah diketahui bahwa kecepatan kipas hanya bisa ditambah per 10%, dan bahwa kurva kipas bawaan firmware tidak berpengaruh apa pun pada model itu. Daftar model yang dump-nya paling dibutuhkan ada di [README](../README-id.md#dump-firmware).

Dengan panduan ini, Anda akan membaca chip flash BIOS laptop dari USB Linux live dan menyimpan isinya dalam satu file. Pada AN515-57, file itu juga berisi firmware embedded controller (EC), yang mengatur kipas dan lampu keyboard. **Tidak ada yang ditulis ke laptop.** Prosesnya sekitar setengah jam.

## Yang Anda perlukan

- USB flash drive 4 GB atau lebih untuk Linux (isinya akan dihapus).
- Tempat untuk menyimpan dump: USB flash drive kedua, atau partisi laptop yang bukan drive Windows, misalnya drive data `D:`.
- Laptop tersambung ke listrik sepanjang waktu.

> [!IMPORTANT]
> **BitLocker.** Banyak laptop dijual dengan drive Windows terenkripsi (*Enkripsi perangkat*). Setelah Secure Boot dimatikan, Windows akan meminta kunci pemulihan BitLocker saat laptop dinyalakan berikutnya. Sebelum mulai, cari kunci Anda (ada di akun Microsoft Anda di [aka.ms/myrecoverykey](https://aka.ms/myrecoverykey)) atau tangguhkan BitLocker sampai Anda menyalakannya lagi. Caranya, ketik perintah ini di terminal yang dijalankan sebagai administrator:
>
> ```powershell
> manage-bde -protectors -disable C: -RebootCount 0
> ```

## 1. Buat USB Linux

Unduh **Linux Mint** (edisi Cinnamon) dari [linuxmint.com](https://linuxmint.com/download.php) lalu tulis image-nya ke USB dengan [Rufus](https://rufus.ie). Pengaturan bawaannya tidak perlu diubah.

## 2. Matikan Secure Boot

Selama Secure Boot menyala, Linux tidak mengizinkan program mengakses perangkat keras secara langsung, padahal tanpa itu chip tidak bisa dibaca. BIOS tidak tersedia dalam bahasa Indonesia, jadi nama opsi di bawah ditulis dalam bahasa Inggris seperti di layar.

1. Mulai ulang laptop dan tekan **F2** berulang kali saat logo Acer muncul untuk membuka pengaturan BIOS.
2. Acer baru mengizinkan **Secure Boot** diubah jika sudah ada kata sandi supervisor. Di tab **Security**, pilih **Set Supervisor Password**, lalu buat kata sandi.
3. Di tab **Boot**, setel **Secure Boot** ke **Disabled**.
4. Di tab **Main**, setel **F12 Boot Menu** ke **Enabled** jika belum.
5. Tekan **F10** untuk menyimpan dan memulai ulang.

## 3. Jalankan Linux dari USB

Colokkan USB, mulai ulang, dan tekan **F12** saat logo Acer muncul. Pilih USB, lalu **Start Linux Mint**. Linux berjalan langsung dari USB dan tidak menyentuh Windows.

## 4. Pasang flashrom

Sambungkan ke Wi-Fi atau kabel jaringan (ikon jaringan di kanan bawah), buka **Terminal**, lalu ketik:

```sh
sudo apt update
sudo apt install -y flashrom
```

## 5. Baca chip

Pertama, biarkan flashrom mencari chip. Perintah ini belum membaca apa pun:

```sh
sudo flashrom -p internal
```

Perintah ini menampilkan nama dan ukuran chip, lalu berhenti dengan peringatan bahwa ini laptop. Itu normal: secara bawaan flashrom menolak berjalan di laptop agar tidak ada yang tertulis secara tidak sengaja, dan di sini kita hanya membaca. Jika flashrom menemukan beberapa chip yang cocok, tambahkan `-c "NAMA"` dengan salah satu nama itu ke perintah di bawah.

Sekarang baca chip dua kali dan bandingkan kedua salinannya:

```sh
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios1.bin 2>&1 | tee ~/flashrom.txt
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios2.bin
cmp ~/bios1.bin ~/bios2.bin && echo "DUMP OK" || echo "DIFFERENT, read again"
```

Seharusnya muncul `DUMP OK`. Jika kedua salinan berbeda, ulangi kedua perintah baca. Bagian besar file yang isinya `FF` semua (wilayah Intel ME) itu normal.

Jika flashrom sama sekali tidak bisa membaca chip (ini bisa terjadi di beberapa laptop AMD), simpan `flashrom.txt`: pesan kesalahan di dalamnya juga berguna.

## 6. Simpan dump

Sistem live menyimpan file di RAM, jadi file itu hilang saat laptop dimatikan. Buka aplikasi **Files** dan klik USB kedua atau drive data Anda di bilah samping untuk memasangnya. Lalu lihat di mana drive itu dipasang:

```sh
lsblk -o NAME,SIZE,FSTYPE,LABEL,MOUNTPOINTS
```

Salin file ke folder itu (jalur di kolom `MOUNTPOINTS`, misalnya `/media/mint/Data`):

```sh
cp ~/bios1.bin ~/flashrom.txt /media/mint/Data/
sync
```

## 7. Kembalikan seperti semula

1. Mulai ulang, cabut USB, dan tekan **F2** untuk membuka pengaturan BIOS lagi.
2. Di tab **Boot**, setel **Secure Boot** kembali ke **Enabled**.
3. Di tab **Security**, pilih **Set Supervisor Password**, masukkan kata sandi saat ini, lalu biarkan kolom kata sandi baru kosong agar kata sandi terhapus.
4. Tekan **F10** untuk menyimpan dan masuk ke Windows.

Jika Anda menangguhkan BitLocker, nyalakan lagi dengan mengetik perintah ini di terminal yang dijalankan sebagai administrator:

```powershell
manage-bde -protectors -enable C:
```

## 8. Kirim dump

Yang diperlukan hanya `bios1.bin` (dan `flashrom.txt` jika pembacaan gagal). [Buka issue](https://github.com/archivesteak/opensense/issues), tulis model dan versi BIOS laptop Anda (OpenSense menampilkan keduanya di **Pengaturan → Laptop Anda**), lalu lampirkan dump. GitHub menerima file hingga 25 MB, dan hanya jenis tertentu: jika dump lebih besar atau ditolak GitHub, kompres dulu ke arsip `.zip`. Jika arsipnya masih terlalu besar, unggah ke layanan berbagi file mana pun dan tempelkan tautannya.

> [!WARNING]
> Issue bersifat publik, dan dump berisi detail tentang laptop Anda: nomor serinya, kunci lisensi Windows yang disimpan Acer di firmware, dan kata sandi supervisor yang Anda buat di langkah 2 (karena itu, sebaiknya gunakan kata sandi yang tidak Anda pakai di tempat lain). Jika Anda tidak ingin memublikasikannya, kirim zip (atau tautannya) lewat email ke [archivesteak@gmail.com](mailto:archivesteak@gmail.com), beserta model dan versi BIOS laptop Anda.
