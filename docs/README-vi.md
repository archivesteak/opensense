<div align="center">

![OpenSense](../resources/banner.png)

**Điều khiển quạt, hiệu năng và đèn cho laptop Acer Nitro và Predator.**<br>
Giải pháp mã nguồn mở thay thế NitroSense và PredatorSense.

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&logo=gnu&color=C4282D)](../LICENSE)

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
- **Điều khiển quạt.** Tự động, được OpenSense tăng tốc khi máy nóng (có thể tắt), Tối đa, hoặc Tùy chỉnh: tốc độ cộng thêm cho từng quạt, cố định hoặc theo đường cong do bạn tự vẽ. Trên các máy có các tính năng này, đường cong quạt của chính firmware có thể nhanh hơn và DustDefender thổi bụi ra.
- **An toàn theo mặc định.** Chống bóp xung đẩy quạt lên hết tốc độ khi bộ xử lý gần đến ngưỡng bóp xung, và firmware sẽ tiếp quản lại nếu cảm biến hoặc OpenSense ngừng hoạt động.
- **Hiệu năng.** Chế độ hoạt động, CoolBoost, ép xung GPU cho từng chế độ và kế hoạch nguồn điện của Windows. Phím chế độ chuyển qua lại giữa các chế độ, và khi dùng pin có chế độ riêng. Trên các mẫu Predator từ 2024 trở đi, mức ép xung GPU riêng của Acer cho từng chế độ cũng được cộng thêm.
- **Đèn.** Màu tĩnh cho từng vùng bàn phím, hoặc các hiệu ứng Nhịp thở, Neon, Sóng, Dịch chuyển, Phóng to, Sao băng và Lấp lánh. Cả thanh đèn, Infinity Mirror, InfiniteRing, logo, phím Turbo và phím chế độ, mỗi thứ có hiệu ứng riêng. Bàn phím có đèn theo từng phím và phím MagForce nhận màu riêng cho từng phím, cùng các hiệu ứng của riêng chúng.
- **Bàn phím và màn hình.** Tự động tắt đèn nền, khóa phím Windows, LCD overdrive và công tắc GPU (MUX).
- **Pin.** Tình trạng pin (dung lượng còn lại và số chu kỳ sạc), dừng sạc ở 80%, hiệu chỉnh pin và sạc thiết bị USB khi tắt máy.
- **Khởi động.** Hoạt ảnh và âm thanh khi khởi động, cùng logo khởi động của riêng bạn trên các máy hỗ trợ.
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

### Bản sao firmware

Máy của bạn thiếu tính năng nào đó, hoặc có tính năng chạy không đúng? Hãy gửi một bản sao firmware của máy: bản sao sẽ được phân tích để tìm hiểu tính năng đó hoạt động thế nào trên mẫu máy của bạn. Quạt của AN515-57 đã được tìm hiểu theo cách này: firmware của máy cho thấy tốc độ quạt chỉ tăng được theo từng bước 10%, và đường cong quạt có sẵn trong firmware không có tác dụng gì. [Hướng dẫn này](firmware/dump-firmware-vi.md) giải thích cách tạo bản sao từ một USB Linux mà không thay đổi gì trên máy.

Bản sao chỉ lấy từ chip firmware của máy: trong đó không có tệp, tài khoản hay bất cứ thứ gì khác của Windows. Thông tin cá nhân duy nhất trong đó là số sê-ri của máy và khóa bản quyền Windows mà Acer lưu trong firmware; hướng dẫn cũng nói cách gửi riêng nếu bạn không muốn công khai những thông tin này.

Những mẫu máy sau sẽ giúp ích nhiều nhất:

- **Nitro AN515-46, AN515-47, AN515-58, AN517-42, AN517-43 và AN517-55**: những mẫu duy nhất mà phần mềm của Acer đặt **Đường cong quạt**, nên cũng là những mẫu duy nhất OpenSense hiển thị tùy chọn này. Chưa ai kiểm tra nó thay đổi gì trên các mẫu đó.
- **Predator Helios 16 và 18 đời 2024 và 2025 (PH16-72, PH18-72, PH16-73, PH18-73) và Helios Neo 16 (PHN16-72)**: trên các mẫu Predator từ 2024 trở đi, chế độ hoạt động và mức ép xung GPU của Acer đi qua giao diện HID của bộ điều khiển nhúng, mà OpenSense điều khiển chỉ dựa trên phần mềm của Acer.
- **Predator Helios 16 và 18 đời 2023 (PH16-71, PH18-71) và Helios 3D 15 (PH3D15-71)**: thanh đèn phía sau, có hiệu ứng do bộ điều khiển nhúng tạo ra.
- **Bất kỳ mẫu máy nào khác**: OpenSense tăng tốc quạt theo từng bước 10% trên mọi máy, vì bộ điều khiển của AN515-57 bỏ qua mọi giá trị ở giữa. Một bản sao sẽ cho thấy máy của bạn có làm như vậy không.

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
