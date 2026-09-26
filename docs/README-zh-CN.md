<div align="center">

![OpenSense](../resources/banner.png)

**适用于 Acer Nitro 和 Predator 笔记本电脑的风扇、性能和灯光控制。**<br>
NitroSense 和 PredatorSense 的开源替代品。

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&logo=gnu&color=C4282D)](../LICENSE)

[**下载**](https://github.com/archivesteak/opensense/releases/latest) •
[功能](#功能) •
[支持的笔记本电脑](#支持的笔记本电脑) •
[构建](#构建)

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
**简体中文** |
[繁體中文](README-zh-TW.md)

欢迎帮助将 OpenSense 和本页翻译成你的语言：请参阅[翻译](#翻译)。

</div>

## 功能

- **真实温度。** CPU 和 GPU 温度直接从芯片本身读取，与 HWiNFO 和 ThrottleStop 的做法相同，并提供 5 分钟曲线图。
- **风扇控制。** “自动”：笔记本发热时由 OpenSense 额外提速（可关闭）；“最大”；或“自定义”：为每个风扇额外增加转速，固定或按你自己绘制的曲线变化。在具备这些功能的笔记本上，还可以把固件自带的风扇曲线调得更快，DustDefender 也可以吹出灰尘。
- **默认安全。** “防降频”会在处理器接近降频温度时把风扇提到全速；如果传感器或 OpenSense 停止工作，固件会重新接管风扇。
- **性能。** 操作模式、CoolBoost、按模式设置的 GPU 超频和 Windows 电源计划。模式键可在各模式间切换，使用电池时另有单独的模式。2024 年及以后的 Predator 还会叠加 Acer 为每种模式设定的 GPU 超频。
- **灯光。** 按键盘区域设置静态颜色，或使用“呼吸”、“霓虹”、“波浪”、“移动”、“缩放”、“流星”和“闪烁”效果。还有灯条、Infinity Mirror、InfiniteRing、徽标以及 Turbo 键和模式键，各有自己的效果。单键背光键盘和 MagForce 键可为每个键设置颜色，并有自己的效果。
- **键盘和显示器。** 自动关闭背光、锁定 Windows 键、LCD Overdrive 以及 GPU（MUX）切换。
- **电池。** 电池健康状况（剩余容量和充电循环次数）、充电到 80% 时停止、校准电池以及关机时为 USB 设备充电。
- **启动。** 开机动画和声音，以及在支持的笔记本电脑上使用自己的开机徽标。
- **无需管理员权限提示。** 一个小型后台服务从开机起应用你的设置，按 NitroSense 键即可打开应用。
- **你的语言。** 支持 36 种语言，跟随 Windows 或在设置中选择。

OpenSense 会向固件询问你的笔记本电脑具备哪些功能，并且只显示它支持的内容。

## 安装

从 [**Releases**](https://github.com/archivesteak/opensense/releases/latest) 下载最新版本（Windows 10 2004 或更高版本，64 位）：

- **`OpenSense-<version>-Setup-x64.exe`**：安装程序。包含 OpenSense 所需的一切，并会自动保持最新。
- **`OpenSense-<version>-Portable-x64.zip`**：无需安装。它会请求管理员权限，并且只在打开期间控制笔记本电脑。
  它不包含 PawnIO 驱动程序，因此在你从 [PawnIO 的发布页面](https://github.com/namazso/PawnIO.Setup/releases/latest)单独安装之前，CPU 温度来自笔记本电脑的固件，精度较低。

> [!NOTE]
> 使用 OpenSense 之前，请先关闭（或卸载）NitroSense，否则两者会争夺风扇的控制权。

## 支持的笔记本电脑

带有 NitroSense 和 PredatorSense 所使用的游戏固件接口的 Acer 笔记本电脑。

OpenSense 是在 **Nitro 5 AN515-57** 上开发的。

在其他型号上试过了？请[提交 issue](https://github.com/archivesteak/opensense/issues)，并粘贴 **设置 → 疑难解答 → 复制** 中的诊断信息。

### 固件转储

你的笔记本缺少某项功能，或者某项功能工作不正常？请发送一份它的固件副本，我们会分析它，弄清这在你的机型上是怎样实现的。AN515-57 的风扇就是这样弄明白的：它的固件显示，风扇转速只能以 10% 为步长提高，而固件自带的风扇曲线根本不起作用。[这份指南](firmware/dump-firmware-zh-CN.md)介绍了如何用 Linux U 盘制作副本，而不改动笔记本上的任何东西。

副本只来自笔记本的固件芯片：里面没有你的任何文件、账户或 Windows 中的其他内容。其中仅有的个人信息是笔记本的序列号，以及 Acer 存放在固件中的 Windows 许可证密钥；如果你不想公开这些信息，指南也介绍了如何私下发送。

最有帮助的机型：

- **Nitro AN515-46、AN515-47、AN515-58、AN517-42、AN517-43 和 AN517-55**：Acer 软件只在这些机型上设置 **风扇曲线**，因此 OpenSense 也只在这些机型上显示它。还没有人核实过它在这些机型上改变了什么。
- **2024 年和 2025 年的 Predator Helios 16 和 18（PH16-72、PH18-72、PH16-73、PH18-73）以及 Helios Neo 16（PHN16-72）**：在 2024 年及以后的 Predator 上，操作模式和 Acer 的 GPU 超频都经由嵌入式控制器的 HID 接口，而 OpenSense 只是依据 Acer 的软件来控制它。
- **2023 年的 Predator Helios 16 和 18（PH16-71、PH18-71）以及 Helios 3D 15（PH3D15-71）**：后部灯条，其效果由嵌入式控制器生成。
- **其他任何机型**：由于 AN515-57 的控制器会丢弃介于其间的数值，OpenSense 在所有笔记本上都以 10% 为步长为风扇提速。转储可以显示你的机型是否也是如此。

## 翻译

OpenSense 使用 Windows 的显示语言，或在 **设置 → 外观 → 语言** 中选择的语言。所有翻译（包括本页）尚未经过母语人士审校，欢迎指正。

应用的文本位于 `src/OpenSense.App/Strings/<language>/Resources.resw`，安装程序的文本位于 `installer/Strings/<language>.nsh`，英文文件中的每个字符串都附有说明；`dotnet test` 会检查每种翻译是否包含所有字符串和占位符。本页的各语言版本位于 [`docs`](.)。

## 构建

需要在 Windows 上安装 [.NET 10 SDK](https://dotnet.microsoft.com/download)。

```powershell
dotnet build
dotnet test --project tests/OpenSense.Core.Tests
```

每次推送都会由 [GitHub Actions](../.github/workflows/build.yml) 构建、测试和打包，其中也展示了如何用 [NSIS](https://nsis.sourceforge.io) 制作安装程序。推送 `v1.2.3` 这样的标签会发布新版本。

## 致谢

- [PawnIO](https://pawnio.eu)：读取 CPU 温度的已签名驱动程序（其模块采用 LGPL-2.1 许可）
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK)、[Win2D](https://github.com/microsoft/Win2D) 和 [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon)、[StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc) 和 [Serilog](https://serilog.net)

## 许可证

OpenSense 采用 [GNU 通用公共许可证 v3.0 或更高版本](../LICENSE)授权。

这是一个独立项目，基于互操作性分析编写，不包含任何 Acer 代码。它与 Acer 没有关联，也未获得 Acer 的认可。Acer、Nitro、Predator 和 NitroSense 是 Acer Inc. 的商标。
