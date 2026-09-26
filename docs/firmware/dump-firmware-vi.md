# Sao chép firmware của máy tính xách tay

[English](dump-firmware.md) |
[Bahasa Indonesia](dump-firmware-id.md) |
[Deutsch](dump-firmware-de.md) |
[Español](dump-firmware-es.md) |
[Français](dump-firmware-fr.md) |
[Polski](dump-firmware-pl.md) |
[Português (Brasil)](dump-firmware-pt-BR.md) |
**Tiếng Việt** |
[Türkçe](dump-firmware-tr.md) |
[Русский](dump-firmware-ru.md) |
[Українська](dump-firmware-uk.md) |
[简体中文](dump-firmware-zh-CN.md) |
[繁體中文](dump-firmware-zh-TW.md)

Nếu máy của bạn thiếu một tính năng hoặc tính năng đó chạy không đúng, bản sao firmware của máy sẽ cho thấy tính năng đó thực sự hoạt động thế nào trên mẫu máy của bạn. Phần lớn những gì OpenSense biết về quạt đến từ firmware của Nitro 5 AN515-57: nhờ đó mới biết tốc độ quạt chỉ tăng được theo từng bước 10%, và đường cong quạt có sẵn trong firmware không có tác dụng gì trên mẫu máy đó. Danh sách các mẫu máy cần bản sao nhất có trong [README](../README-vi.md#bản-sao-firmware).

Với hướng dẫn này, bạn sẽ đọc chip flash BIOS của máy từ một USB Linux live và lưu nội dung của nó thành một tệp duy nhất. Trên AN515-57, tệp đó cũng chứa firmware của bộ điều khiển nhúng (EC), bộ phận điều khiển quạt và đèn bàn phím. **Không có gì được ghi vào máy.** Toàn bộ mất khoảng nửa giờ.

## Bạn cần gì

- Một USB từ 4 GB trở lên cho Linux (dữ liệu trên đó sẽ bị xóa).
- Một nơi để lưu bản sao: một USB thứ hai, hoặc một phân vùng trên máy không phải ổ Windows, chẳng hạn ổ dữ liệu `D:`.
- Máy cắm sạc trong suốt quá trình.

> [!IMPORTANT]
> **BitLocker.** Nhiều máy được bán ra với ổ Windows đã mã hóa (*Mã hóa thiết bị*). Tắt Secure Boot khiến Windows hỏi khóa khôi phục BitLocker ở lần khởi động sau. Trước khi bắt đầu, hãy tìm khóa của bạn (có trong tài khoản Microsoft của bạn tại [aka.ms/myrecoverykey](https://aka.ms/myrecoverykey)) hoặc tạm dừng BitLocker cho đến khi bạn bật lại. Để làm vậy, hãy nhập lệnh sau trong một terminal chạy với quyền quản trị viên:
>
> ```powershell
> manage-bde -protectors -disable C: -RebootCount 0
> ```

## 1. Tạo USB Linux

Tải **Linux Mint** (bản Cinnamon) từ [linuxmint.com](https://linuxmint.com/download.php) và ghi vào USB bằng [Rufus](https://rufus.ie). Không cần đổi thiết lập mặc định.

## 2. Tắt Secure Boot

Khi Secure Boot bật, Linux không cho chương trình truy cập trực tiếp vào phần cứng, mà thiếu quyền đó thì không đọc được chip. BIOS không có tiếng Việt, nên tên các mục dưới đây được giữ nguyên tiếng Anh như trên màn hình.

1. Khởi động lại máy và nhấn **F2** liên tục khi logo Acer hiện ra để mở thiết lập BIOS.
2. Acer chỉ cho phép đổi **Secure Boot** khi đã có mật khẩu supervisor. Trong thẻ **Security**, chọn **Set Supervisor Password** và đặt một mật khẩu.
3. Trong thẻ **Boot**, đặt **Secure Boot** thành **Disabled**.
4. Trong thẻ **Main**, đặt **F12 Boot Menu** thành **Enabled** nếu chưa bật.
5. Nhấn **F10** để lưu và khởi động lại.

## 3. Khởi động Linux từ USB

Cắm USB, khởi động lại và nhấn **F12** khi logo Acer hiện ra. Chọn USB, rồi chọn **Start Linux Mint**. Linux chạy thẳng từ USB và không động đến Windows.

## 4. Cài flashrom

Kết nối Wi-Fi hoặc cắm cáp mạng (biểu tượng mạng ở góc dưới bên phải), mở **Terminal** và nhập:

```sh
sudo apt update
sudo apt install -y flashrom
```

## 5. Đọc chip

Trước tiên để flashrom tìm chip. Lệnh này chưa đọc gì cả:

```sh
sudo flashrom -p internal
```

Lệnh này hiển thị tên và dung lượng chip, rồi dừng với cảnh báo rằng đây là máy tính xách tay. Điều đó là bình thường: theo mặc định flashrom không chạy trên máy tính xách tay để tránh vô tình ghi vào chip, còn ở đây ta chỉ đọc. Nếu flashrom tìm thấy nhiều chip phù hợp, hãy thêm `-c "TÊN"` với một trong các tên đó vào các lệnh bên dưới.

Giờ đọc chip hai lần và so sánh hai bản sao:

```sh
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios1.bin 2>&1 | tee ~/flashrom.txt
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios2.bin
cmp ~/bios1.bin ~/bios2.bin && echo "DUMP OK" || echo "DIFFERENT, read again"
```

Kết quả phải là `DUMP OK`. Nếu hai bản sao khác nhau, hãy chạy lại hai lệnh đọc. Có những vùng lớn chỉ toàn `FF` (vùng Intel ME) là bình thường.

Nếu flashrom hoàn toàn không đọc được chip (điều này có thể xảy ra trên một số máy AMD), hãy giữ `flashrom.txt`: thông báo lỗi trong đó cũng có ích.

## 6. Lưu bản sao

Hệ thống live giữ tệp trong RAM, nên chúng sẽ mất khi tắt máy. Mở ứng dụng **Files** và bấm vào USB thứ hai hoặc ổ dữ liệu của bạn ở thanh bên để gắn nó. Sau đó xem nó được gắn ở đâu:

```sh
lsblk -o NAME,SIZE,FSTYPE,LABEL,MOUNTPOINTS
```

Chép các tệp vào thư mục đó (đường dẫn trong cột `MOUNTPOINTS`, ví dụ `/media/mint/Data`):

```sh
cp ~/bios1.bin ~/flashrom.txt /media/mint/Data/
sync
```

## 7. Khôi phục như cũ

1. Khởi động lại, rút USB và nhấn **F2** để mở lại thiết lập BIOS.
2. Trong thẻ **Boot**, đặt **Secure Boot** lại thành **Enabled**.
3. Trong thẻ **Security**, chọn **Set Supervisor Password**, nhập mật khẩu hiện tại và để trống ô mật khẩu mới: như vậy mật khẩu sẽ bị xóa.
4. Nhấn **F10** để lưu và khởi động lại vào Windows.

Nếu bạn đã tạm dừng BitLocker, hãy bật lại bằng lệnh sau trong một terminal chạy với quyền quản trị viên:

```powershell
manage-bde -protectors -enable C:
```

## 8. Gửi bản sao

Chỉ cần `bios1.bin` (và `flashrom.txt` nếu đọc không thành công). Hãy [mở một issue](https://github.com/archivesteak/opensense/issues), ghi mẫu máy và phiên bản BIOS của bạn (OpenSense hiển thị cả hai trong **Cài đặt → Máy tính xách tay của bạn**) và đính kèm bản sao. GitHub nhận tệp tối đa 25 MB và chỉ một số loại: nếu bản sao lớn hơn hoặc GitHub từ chối, hãy nén nó thành tệp `.zip` trước. Nếu tệp nén vẫn quá lớn, hãy tải nó lên một dịch vụ chia sẻ tệp bất kỳ và dán liên kết.

> [!WARNING]
> Issue là công khai, và bản sao chứa thông tin về máy của bạn: số sê-ri, khóa bản quyền Windows mà Acer lưu trong firmware và mật khẩu supervisor bạn đặt ở bước 2 (vì vậy nên dùng mật khẩu bạn không dùng ở nơi nào khác). Nếu không muốn công khai những thông tin này, hãy gửi tệp zip (hoặc liên kết đến nó) qua email tới [archivesteak@gmail.com](mailto:archivesteak@gmail.com), kèm mẫu máy và phiên bản BIOS.
