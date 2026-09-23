<div align="center">

![OpenSense](../resources/banner.png)

**适用于 Acer Nitro 和 Predator 笔记本电脑的风扇、性能和灯光控制。**<br>
NitroSense 的开源替代品。

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&color=E4473C)](../LICENSE)

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
- **风扇控制。** “自动”、“最大”、每个风扇的固定转速，或者你自己绘制的温度曲线。
- **默认安全。** 达到紧急温度时风扇会全速运转；如果传感器或 OpenSense 停止工作，固件会重新接管风扇。
- **性能。** 操作模式、CoolBoost 和 Windows 电源计划。
- **灯光。** 按键盘区域设置静态颜色，或使用“呼吸”、“霓虹”、“波浪”、“移动”和“缩放”效果。
- **键盘和显示器。** 自动关闭背光、锁定 Windows 键、LCD Overdrive 以及 GPU（MUX）切换。
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

带有 NitroSense 和 PredatorSense 所使用的游戏固件接口的 Acer 笔记本电脑。OpenSense 在 **Nitro 5 AN515-57** 上开发。

在其他型号上试过了？请[提交 issue](https://github.com/archivesteak/opensense/issues)，并粘贴 **设置 → 疑难解答 → 复制** 中的诊断信息。

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
