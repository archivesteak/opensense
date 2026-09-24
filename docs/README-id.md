<div align="center">

![OpenSense](../resources/banner.png)

**Kontrol kipas, performa, dan pencahayaan untuk laptop Acer Nitro dan Predator.**<br>
Pengganti NitroSense yang bersumber terbuka.

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&color=E4473C)](../LICENSE)

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
- **Kontrol kipas.** Otomatis, yang dipercepat OpenSense saat laptop panas (bisa Anda matikan), Maks, atau Kustom: kecepatan tambahan per kipas, tetap atau mengikuti kurva yang Anda gambar sendiri.
- **Aman secara default.** Anti-throttling menaikkan kipas hingga kecepatan penuh saat prosesor mendekati titik throttling, dan firmware kembali mengambil alih jika sensor atau OpenSense berhenti.
- **Performa.** Mode operasi, CoolBoost, dan rencana daya Windows.
- **Pencahayaan.** Warna statis per zona keyboard, atau efek Bernapas, Neon, Gelombang, Bergeser, dan Zoom.
- **Keyboard dan layar.** Lampu latar mati otomatis, kunci tombol Windows, Overdrive LCD, dan sakelar GPU (MUX).
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
