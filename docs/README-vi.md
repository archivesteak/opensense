<div align="center">

![OpenSense](../resources/banner.png)

**Điều khiển quạt, hiệu năng và đèn cho laptop Acer Nitro và Predator.**<br>
Giải pháp mã nguồn mở thay thế NitroSense.

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&color=E4473C)](../LICENSE)

[**Tải xuống**](https://github.com/archivesteak/opensense/releases/latest) •
[Tính năng](#tính-năng) •
[Laptop được hỗ trợ](#laptop-được-hỗ-trợ) •
[Biên dịch](#biên-dịch)

[English](../README.md) |
[Bahasa Indonesia](README-id.md) |
[Deutsch](README-de.md) |
[Español](README-es.md) |
[Français](README-fr.md) |
[Polski](README-pl.md) |
[Português (Brasil)](README-pt-BR.md) |
**Tiếng Việt** |
[Türkçe](README-tr.md) |
[Русский](README-ru.md) |
[Українська](README-uk.md) |
[简体中文](README-zh-CN.md) |
[繁體中文](README-zh-TW.md)

Hãy giúp dịch OpenSense và trang này sang ngôn ngữ của bạn: xem [Bản dịch](#bản-dịch).

</div>

## Tính năng

- **Nhiệt độ thực.** Nhiệt độ CPU và GPU được đọc trực tiếp từ chính các chip, giống như HWiNFO và ThrottleStop, kèm biểu đồ 5 phút.
- **Điều khiển quạt.** Tự động, được OpenSense tăng tốc khi máy nóng (có thể tắt), Tối đa, hoặc Tùy chỉnh: tốc độ cộng thêm cho từng quạt, cố định hoặc theo đường cong do bạn tự vẽ.
- **An toàn theo mặc định.** Chống bóp xung đẩy quạt lên hết tốc độ khi bộ xử lý gần đến ngưỡng bóp xung, và firmware sẽ tiếp quản lại nếu cảm biến hoặc OpenSense ngừng hoạt động.
- **Hiệu năng.** Chế độ hoạt động, CoolBoost và kế hoạch nguồn điện của Windows.
- **Đèn.** Màu tĩnh cho từng vùng bàn phím, hoặc các hiệu ứng Nhịp thở, Neon, Sóng, Dịch chuyển và Phóng to.
- **Bàn phím và màn hình.** Tự động tắt đèn nền, khóa phím Windows, LCD overdrive và công tắc GPU (MUX).
- **Không có lời nhắc quản trị viên.** Một dịch vụ nền nhỏ áp dụng cài đặt của bạn ngay từ khi khởi động, và phím NitroSense sẽ mở ứng dụng.
- **Ngôn ngữ của bạn.** 36 ngôn ngữ, theo Windows hoặc chọn trong phần cài đặt.

OpenSense hỏi firmware xem laptop của bạn có những gì và chỉ hiển thị những gì được hỗ trợ.

## Cài đặt

Tải phiên bản mới nhất từ [**Releases**](https://github.com/archivesteak/opensense/releases/latest) (Windows 10 2004 trở lên, 64-bit):

- **`OpenSense-<version>-Setup-x64.exe`**: trình cài đặt. Bao gồm mọi thứ OpenSense cần và tự cập nhật.
- **`OpenSense-<version>-Portable-x64.zip`**: không cần cài đặt. Yêu cầu quyền quản trị viên và chỉ điều khiển laptop khi đang mở.
  Gói này không kèm trình điều khiển PawnIO, nên cho đến khi bạn cài riêng từ [trang phát hành của PawnIO](https://github.com/namazso/PawnIO.Setup/releases/latest), nhiệt độ CPU sẽ lấy từ firmware của laptop, vốn kém chính xác hơn.

> [!NOTE]
> Hãy tắt NitroSense (hoặc gỡ cài đặt) trước khi dùng OpenSense, nếu không hai phần mềm sẽ tranh nhau điều khiển quạt.

## Laptop được hỗ trợ

Laptop Acer có giao diện firmware dành cho chơi game mà NitroSense và PredatorSense sử dụng.

OpenSense đã được phát triển trên một chiếc **Nitro 5 AN515-57**.

Đã thử trên mẫu máy khác? Hãy [mở một issue](https://github.com/archivesteak/opensense/issues) và dán thông tin chẩn đoán từ **Cài đặt → Khắc phục sự cố → Sao chép**.

## Bản dịch

OpenSense dùng ngôn ngữ hiển thị của Windows, hoặc ngôn ngữ được chọn trong **Cài đặt → Giao diện → Ngôn ngữ**. Chưa có người bản ngữ nào kiểm tra các bản dịch, kể cả trang này, vì vậy mọi góp ý sửa lỗi đều được hoan nghênh.

Văn bản của ứng dụng nằm trong `src/OpenSense.App/Strings/<language>/Resources.resw` và của trình cài đặt nằm trong `installer/Strings/<language>.nsh`, với ghi chú cho từng chuỗi trong các tệp tiếng Anh; `dotnet test` kiểm tra rằng mỗi bản dịch có đủ mọi chuỗi và phần giữ chỗ. Các bản dịch của trang này nằm trong [`docs`](.).

## Biên dịch

Bạn cần [.NET 10 SDK](https://dotnet.microsoft.com/download) trên Windows.

```powershell
dotnet build
dotnet test --project tests/OpenSense.Core.Tests
```

Mỗi lần push đều được [GitHub Actions](../.github/workflows/build.yml) biên dịch, kiểm thử và đóng gói; tại đó cũng có thể thấy cách trình cài đặt được tạo bằng [NSIS](https://nsis.sourceforge.io). Đẩy một thẻ `v1.2.3` sẽ phát hành một phiên bản.

## Ghi công

- [PawnIO](https://pawnio.eu): trình điều khiển đã ký dùng để đọc nhiệt độ CPU (các mô-đun của nó dùng giấy phép LGPL-2.1)
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK), [Win2D](https://github.com/microsoft/Win2D) và [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon), [StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc) và [Serilog](https://serilog.net)

## Giấy phép

OpenSense được cấp phép theo [GNU General Public License v3.0 trở lên](../LICENSE).

Đây là một dự án độc lập, được viết dựa trên phân tích khả năng tương tác, và không chứa mã nào của Acer. Dự án không liên kết với Acer và không được Acer xác nhận. Acer, Nitro, Predator và NitroSense là nhãn hiệu của Acer Inc.
