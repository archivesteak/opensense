<div align="center">

![OpenSense](../resources/banner.png)

**適用於 Acer Nitro 與 Predator 筆記型電腦的風扇、效能與燈光控制。**<br>
NitroSense 與 PredatorSense 的開放原始碼替代方案。

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&logo=gnu&color=C4282D)](../LICENSE)

[**下載**](https://github.com/archivesteak/opensense/releases/latest) •
[功能](#功能) •
[支援的筆記型電腦](#支援的筆記型電腦) •
[建置](#建置)

[English](../README.md) |
[Bahasa Indonesia](README-id.md) |
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
**繁體中文**

歡迎協助將 OpenSense 與本頁翻譯成你的語言：請參閱[翻譯](#翻譯)。

</div>

## 功能

- **真實溫度。** CPU 與 GPU 溫度直接從晶片本身讀取，做法與 HWiNFO 和 ThrottleStop 相同，並提供 5 分鐘圖表。
- **風扇控制。** 「自動」：筆電發熱時由 OpenSense 額外提速（可關閉）；「最大」；或「自訂」：為每個風扇額外增加轉速，固定或依你自己畫出的曲線變化。在具備這些功能的筆電上，還可以把韌體內建的風扇曲線調得更快，DustDefender 也可以吹出灰塵。
- **預設安全。** 「防降頻」會在處理器接近降頻溫度時把風扇拉到全速；若感應器或 OpenSense 停止運作，韌體會重新接手風扇。
- **效能。** 作業模式、CoolBoost、依模式設定的 GPU 超頻與 Windows 電源計劃。模式鍵可在各模式間切換，使用電池時另有獨立的模式。2024 年及之後的 Predator 還會疊加 Acer 為每種模式設定的 GPU 超頻。
- **燈光。** 依鍵盤區域設定靜態色彩，或使用「呼吸」、「霓虹」、「波浪」、「移動」、「縮放」、「流星」與「閃爍」效果。還有燈條、Infinity Mirror、InfiniteRing、標誌以及 Turbo 鍵與模式鍵，各有自己的效果。單鍵背光鍵盤與 MagForce 鍵可為每個鍵設定色彩，並有自己的效果。
- **鍵盤與顯示器。** 自動關閉背光、鎖定 Windows 鍵、LCD Overdrive 以及 GPU（MUX）切換。
- **電池。** 電池健康狀態（剩餘容量與充電循環次數）、充電到 80% 時停止、校準電池以及關機時為 USB 裝置充電。
- **開機。** 開機動畫與音效，以及在支援的筆電上使用自己的開機標誌。
- **不會跳出系統管理員權限提示。** 一個小型背景服務從開機起就套用你的設定，按下 NitroSense 鍵即可開啟應用程式。
- **你的語言。** 支援 36 種語言，跟隨 Windows 或在設定中選擇。

OpenSense 會向韌體詢問你的筆記型電腦具備哪些功能，並只顯示它支援的項目。

## 安裝

從 [**Releases**](https://github.com/archivesteak/opensense/releases/latest) 下載最新版本（Windows 10 2004 或更新版本，64 位元）：

- **`OpenSense-<version>-Setup-x64.exe`**：安裝程式。包含 OpenSense 所需的一切，並會自動保持最新。
- **`OpenSense-<version>-Portable-x64.zip`**：免安裝。它會要求系統管理員權限，且只在開啟期間控制筆記型電腦。
  它不含 PawnIO 驅動程式，因此在你從 [PawnIO 的發行頁面](https://github.com/namazso/PawnIO.Setup/releases/latest)另行安裝之前，CPU 溫度來自筆記型電腦的韌體，準確度較低。

> [!NOTE]
> 使用 OpenSense 之前，請先關閉（或解除安裝）NitroSense，否則兩者會搶奪風扇的控制權。

## 支援的筆記型電腦

具備 NitroSense 與 PredatorSense 所使用之遊戲韌體介面的 Acer 筆記型電腦。

OpenSense 是在 **Nitro 5 AN515-57** 上開發的。

在其他機型上試過了嗎？請[建立 issue](https://github.com/archivesteak/opensense/issues)，並貼上 **設定 → 疑難排解 → 複製** 中的診斷資訊。

### 韌體傾印

你的筆電缺少某項功能，或某項功能運作不正常？請寄送一份它的韌體副本，我們會分析它，弄清楚這在你的機型上是如何運作的。AN515-57 的風扇就是這樣弄明白的：它的韌體顯示，風扇轉速只能以 10% 為單位提高，而韌體內建的風扇曲線完全沒有作用。[這份指南](firmware/dump-firmware-zh-TW.md)說明如何用 Linux USB 隨身碟製作副本，而不更動筆電上的任何東西。

副本只來自筆電的韌體晶片：裡面沒有你的任何檔案、帳戶或 Windows 中的其他內容。其中僅有的個人資訊是筆電的序號，以及 Acer 存放在韌體中的 Windows 授權金鑰；如果你不想公開這些資訊，指南也說明了如何私下寄送。

最有幫助的機型：

- **Nitro AN515-46、AN515-47、AN515-58、AN517-42、AN517-43 與 AN517-55**：Acer 軟體只在這些機型上設定 **風扇曲線**，因此 OpenSense 也只在這些機型上顯示它。還沒有人確認過它在這些機型上改變了什麼。
- **2024 年與 2025 年的 Predator Helios 16 與 18（PH16-72、PH18-72、PH16-73、PH18-73）以及 Helios Neo 16（PHN16-72）**：在 2024 年及之後的 Predator 上，作業模式與 Acer 的 GPU 超頻都經由嵌入式控制器的 HID 介面，而 OpenSense 只是依據 Acer 的軟體來控制它。
- **2023 年的 Predator Helios 16 與 18（PH16-71、PH18-71）以及 Helios 3D 15（PH3D15-71）**：後方燈條，其效果由嵌入式控制器產生。
- **其他任何機型**：由於 AN515-57 的控制器會捨棄介於其間的數值，OpenSense 在所有筆電上都以 10% 為單位為風扇提速。傾印可以顯示你的機型是否也是如此。

## 翻譯

OpenSense 使用 Windows 的顯示語言，或在 **設定 → 外觀 → 語言** 中選擇的語言。所有翻譯（包括本頁）尚未經過母語人士校閱，歡迎指正。

應用程式的文字位於 `src/OpenSense.App/Strings/<language>/Resources.resw`，安裝程式的文字位於 `installer/Strings/<language>.nsh`，英文檔案中的每個字串都附有說明；`dotnet test` 會檢查每個翻譯是否包含所有字串與預留位置。本頁的各語言版本位於 [`docs`](.)。

## 建置

需要在 Windows 上安裝 [.NET 10 SDK](https://dotnet.microsoft.com/download)。

```powershell
dotnet build
dotnet test --project tests/OpenSense.Core.Tests
```

每次推送都會由 [GitHub Actions](../.github/workflows/build.yml) 建置、測試與封裝，其中也說明了如何用 [NSIS](https://nsis.sourceforge.io) 製作安裝程式。推送 `v1.2.3` 這類標籤會發行新版本。

## 致謝

- [PawnIO](https://pawnio.eu)：讀取 CPU 溫度的已簽署驅動程式（其模組採用 LGPL-2.1 授權）
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK)、[Win2D](https://github.com/microsoft/Win2D) 與 [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon)、[StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc) 與 [Serilog](https://serilog.net)

## 授權

OpenSense 採用 [GNU 通用公共授權條款 v3.0 或更新版本](../LICENSE)授權。

這是一個獨立專案，依據互通性分析撰寫，不含任何 Acer 程式碼。它與 Acer 沒有關聯，也未獲得 Acer 認可。Acer、Nitro、Predator 與 NitroSense 是 Acer Inc. 的商標。
