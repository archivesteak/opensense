<div align="center">

![OpenSense](../resources/banner.png)

**Kontrol kipas, performa, dan pencahayaan untuk laptop Acer Nitro dan Predator.**<br>
Pengganti NitroSense dan PredatorSense yang bersumber terbuka.

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&logo=gnu&color=C4282D)](../LICENSE)

[**Unduh**](https://github.com/archivesteak/opensense/releases/latest) •
[Fitur](#fitur) •
[Laptop yang didukung](#laptop-yang-didukung) •
[Membangun](#membangun)

[English](../README.md) |
**Bahasa Indonesia** |
[Deutsch](README-de.md) |
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

Bantu terjemahkan OpenSense dan halaman ini ke bahasa Anda: lihat [Terjemahan](#terjemahan).

</div>

## Fitur

- **Suhu yang sebenarnya.** Suhu CPU dan GPU dibaca langsung dari chip-nya, seperti yang dilakukan HWiNFO dan ThrottleStop, dengan grafik 5 menit.
- **Kontrol kipas.** Otomatis, yang dipercepat OpenSense saat laptop panas (bisa Anda matikan), Maks, atau Kustom: kecepatan tambahan per kipas, tetap atau mengikuti kurva yang Anda gambar sendiri. Pada laptop yang memilikinya, kurva kipas bawaan firmware bisa dibuat lebih cepat dan DustDefender meniup debu keluar.
- **Aman secara default.** Anti-throttling menaikkan kipas hingga kecepatan penuh saat prosesor mendekati titik throttling, dan firmware kembali mengambil alih jika sensor atau OpenSense berhenti.
- **Performa.** Mode operasi, CoolBoost, overclock GPU untuk tiap mode, dan rencana daya Windows. Tombol mode beralih antarmode, dan saat memakai baterai ada mode tersendiri. Pada Predator 2024 dan yang lebih baru, overclock GPU bawaan Acer untuk tiap mode ikut ditambahkan.
- **Pencahayaan.** Warna statis per zona keyboard, atau efek Bernapas, Neon, Gelombang, Bergeser, Zoom, Meteor, dan Kerlip. Juga bilah lampu, Infinity Mirror, InfiniteRing, logo, serta tombol Turbo dan tombol mode, masing-masing dengan efeknya sendiri. Keyboard dengan lampu per tombol dan tombol MagForce bisa diberi warna untuk tiap tombol, dan punya efeknya sendiri.
- **Keyboard dan layar.** Lampu latar mati otomatis, kunci tombol Windows, Overdrive LCD, dan sakelar GPU (MUX).
- **Baterai.** Kesehatan baterai (kapasitas tersisa dan siklus pengisian), pengisian berhenti di 80%, kalibrasi baterai, dan isi daya perangkat USB saat laptop mati.
- **Pengaktifan.** Animasi dan suara saat menyala, serta logo Anda sendiri saat menyala di laptop yang mendukungnya.
- **Tanpa permintaan izin administrator.** Layanan latar belakang kecil menerapkan pengaturan Anda sejak komputer dinyalakan, dan tombol NitroSense membuka aplikasinya.
- **Bahasa Anda.** 36 bahasa, mengikuti Windows atau dipilih di pengaturan.

OpenSense menanyakan kepada firmware apa saja yang dimiliki laptop Anda dan hanya menampilkan yang didukung.

## Instalasi

Unduh versi terbaru dari [**Releases**](https://github.com/archivesteak/opensense/releases/latest) (Windows 10 2004 atau lebih baru, 64-bit):

- **`OpenSense-<version>-Setup-x64.exe`**: penginstal. Berisi semua yang dibutuhkan OpenSense dan memperbarui dirinya sendiri.
- **`OpenSense-<version>-Portable-x64.zip`**: tanpa instalasi. Meminta hak administrator dan hanya mengontrol laptop selama sedang dibuka.
  Paket ini tidak menyertakan driver PawnIO, jadi sampai Anda menginstalnya secara terpisah dari [rilis PawnIO](https://github.com/namazso/PawnIO.Setup/releases/latest), suhu CPU diambil dari firmware laptop, yang kurang akurat.

> [!NOTE]
> Matikan NitroSense (atau copot pemasangannya) sebelum menggunakan OpenSense; jika tidak, keduanya akan berebut kendali kipas.

## Laptop yang didukung

Laptop Acer dengan antarmuka firmware gaming yang digunakan NitroSense dan PredatorSense.

OpenSense dikembangkan di **Nitro 5 AN515-57**.

Sudah mencobanya di model lain? [Buka issue](https://github.com/archivesteak/opensense/issues) dan tempelkan data diagnostik dari **Pengaturan → Pemecahan masalah → Salin**.

### Dump firmware

Ada yang tidak tersedia atau tidak berjalan dengan benar di laptop Anda? Kirimkan salinan firmware-nya, dan salinan itu akan dianalisis untuk mengetahui cara kerjanya pada model Anda. Begitulah cara kerja kipas AN515-57 dipahami: firmware-nya menunjukkan bahwa kecepatan kipas hanya bisa ditambah per 10%, dan kurva kipas bawaan firmware tidak berpengaruh apa pun. [Panduan ini](firmware/dump-firmware-id.md) menjelaskan cara membuat salinan itu dari USB Linux tanpa mengubah apa pun di laptop.

Salinan ini hanya berasal dari chip firmware laptop: tidak ada file, akun, atau apa pun dari Windows di dalamnya. Satu-satunya data pribadi di dalamnya adalah nomor seri laptop dan kunci lisensi Windows yang disimpan Acer di firmware. Jika Anda tidak ingin memublikasikannya, panduan menjelaskan cara mengirim salinan itu secara pribadi.

Model-model ini akan paling membantu:

- **Nitro AN515-46, AN515-47, AN515-58, AN517-42, AN517-43, dan AN517-55**: satu-satunya model tempat perangkat lunak Acer mengatur **Kurva kipas**, jadi satu-satunya model tempat OpenSense menampilkannya. Belum ada yang memeriksa apa yang diubahnya di model-model itu.
- **Predator Helios 16 dan 18 tahun 2024 dan 2025 (PH16-72, PH18-72, PH16-73, PH18-73) serta Helios Neo 16 (PHN16-72)**: pada Predator 2024 dan yang lebih baru, mode operasi dan overclock GPU dari Acer melewati antarmuka HID embedded controller, yang dikendalikan OpenSense hanya berdasarkan perangkat lunak Acer.
- **Predator Helios 16 dan 18 tahun 2023 (PH16-71, PH18-71) serta Helios 3D 15 (PH3D15-71)**: bilah lampu belakang, yang efeknya dihasilkan oleh embedded controller.
- **Model lain apa pun**: OpenSense menambah kecepatan kipas per 10% di semua laptop, karena controller AN515-57 membuang nilai di antaranya. Dump akan menunjukkan apakah laptop Anda melakukan hal yang sama.

## Terjemahan

OpenSense menggunakan bahasa tampilan Windows, atau bahasa yang dipilih di **Pengaturan → Tampilan → Bahasa**. Belum ada penutur asli yang memeriksa terjemahannya, termasuk halaman ini, jadi koreksi sangat diterima.

Teks aplikasi ada di `src/OpenSense.App/Strings/<language>/Resources.resw` dan teks penginstal ada di `installer/Strings/<language>.nsh`, dengan catatan untuk setiap teks di file bahasa Inggris; `dotnet test` memeriksa bahwa setiap terjemahan memiliki semua teks dan placeholder. Terjemahan halaman ini ada di [`docs`](.).

## Membangun

Anda memerlukan [.NET 10 SDK](https://dotnet.microsoft.com/download) di Windows.

```powershell
dotnet build
dotnet test --project tests/OpenSense.Core.Tests
```

Setiap push dibangun, diuji, dan dikemas oleh [GitHub Actions](../.github/workflows/build.yml), yang juga menunjukkan cara penginstal dibuat dengan [NSIS](https://nsis.sourceforge.io). Mendorong tag `v1.2.3` akan menerbitkan rilis.

## Kredit

- [PawnIO](https://pawnio.eu): driver bertanda tangan yang membaca suhu CPU (modulnya berlisensi LGPL-2.1)
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK), [Win2D](https://github.com/microsoft/Win2D), dan [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon), [StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc), dan [Serilog](https://serilog.net)

## Lisensi

OpenSense dilisensikan di bawah [GNU General Public License v3.0 atau yang lebih baru](../LICENSE).

Ini adalah proyek independen, ditulis berdasarkan analisis interoperabilitas, dan tidak berisi kode Acer. Proyek ini tidak berafiliasi dengan Acer dan tidak didukung oleh Acer. Acer, Nitro, Predator, dan NitroSense adalah merek dagang Acer Inc.
