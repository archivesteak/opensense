# 傾印筆電的韌體

[English](dump-firmware.md) |
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
**繁體中文**

如果你的筆電缺少某項功能，或某項功能運作不正常，它的韌體副本就能說明這在你的機型上實際是怎麼運作的。OpenSense 對風扇的了解大多來自 Nitro 5 AN515-57 的韌體：正是這樣才發現風扇轉速只能以 10% 為單位提高，而且韌體內建的風扇曲線在這款機型上沒有作用。最需要的機型清單請見 [README](../README-zh-TW.md#韌體傾印)。

依照本指南，你將從 Linux live USB 隨身碟讀取筆電的 BIOS 快閃記憶體晶片，並把其中的內容儲存為一個檔案。在 AN515-57 上，這個檔案也包含嵌入式控制器（EC）的韌體，它負責控制風扇與鍵盤燈光。**不會對筆電寫入任何內容。** 整個過程大約需要半小時。

## 需要準備

- 一支 4 GB 以上的 USB 隨身碟，用於 Linux（其中的內容會被清除）。
- 儲存傾印檔的地方：第二支 USB 隨身碟，或筆電上不是 Windows 磁碟機的磁碟分割，例如資料磁碟 `D:`。
- 整個過程中筆電一直接上電源。

> [!IMPORTANT]
> **BitLocker。** 許多筆電出廠時 Windows 磁碟機就已加密（*裝置加密*）。關閉 Secure Boot 後，Windows 會在下次啟動時要求輸入 BitLocker 修復金鑰。開始之前，請先找到你的金鑰（在你的 Microsoft 帳戶中，網址是 [aka.ms/myrecoverykey](https://aka.ms/myrecoverykey)），或在以系統管理員身分執行的終端機中暫停 BitLocker，直到你重新開啟它：
>
> ```powershell
> manage-bde -protectors -disable C: -RebootCount 0
> ```

## 1. 製作 Linux USB 隨身碟

從 [linuxmint.com](https://linuxmint.com/download.php) 下載 **Linux Mint**（Cinnamon 版），並用 [Rufus](https://rufus.ie) 把映像檔寫入隨身碟，預設設定不需要更改。

## 2. 關閉 Secure Boot

Secure Boot 開啟時，Linux 不允許程式直接存取硬體，而讀取晶片少不了這種存取。下方的名稱是 BIOS 設定繁體中文版的名稱；如果你的 BIOS 設定顯示英文，請看括號中的名稱。

1. 重新啟動筆電，在 Acer 標誌出現時反覆按 **F2**，進入 BIOS 設定。
2. Acer 只有在設定了監督員密碼後才允許變更 **安全開機**（Secure Boot）。在 **安全性**（Security）索引標籤中選擇 **設定監督員密碼**（Set Supervisor Password）並設定密碼。
3. 在 **開機**（Boot）索引標籤中，將 **安全開機**（Secure Boot）設為 **停用**（Disabled）。
4. 在 **主要**（Main）索引標籤中，如果 **F12 開機選單**（F12 Boot Menu）還不是 **啟用**（Enabled），將其設為 **啟用**（Enabled）。
5. 按 **F10** 儲存並重新啟動。

## 3. 從隨身碟啟動 Linux

插入隨身碟，重新啟動，並在 Acer 標誌出現時按 **F12**。選擇 USB 隨身碟，再選擇 **Start Linux Mint**。Linux 直接從隨身碟執行，不會更動 Windows。

## 4. 安裝 flashrom

連上 Wi-Fi 或網路線（右下角的網路圖示），開啟 **Terminal** 並輸入：

```sh
sudo apt update
sudo apt install -y flashrom
```

## 5. 讀取晶片

先讓 flashrom 偵測晶片。這個命令還不會讀取任何內容：

```sh
sudo flashrom -p internal
```

它會顯示晶片的名稱和容量，接著因為偵測到這是筆電而停下並顯示警告。這是正常的：為了避免誤寫，flashrom 預設不在筆電上執行，而這裡只做讀取。如果它偵測到多個可能的晶片，請在下方的命令中加上 `-c "名稱"`，填入其中一個名稱。

現在讀取晶片兩次，並比較兩份副本：

```sh
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios1.bin 2>&1 | tee ~/flashrom.txt
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios2.bin
cmp ~/bios1.bin ~/bios2.bin && echo "DUMP OK" || echo "DIFFERENT, read again"
```

應該會顯示 `DUMP OK`。如果兩份副本不同，請重新執行這兩條讀取命令。檔案中有大段全是 `FF`（Intel ME 區域），這是正常的。

如果 flashrom 完全無法讀取晶片（部分 AMD 筆電可能發生這種情況），請保留 `flashrom.txt`：其中的錯誤訊息也有用。

## 6. 儲存傾印檔

Live 系統把檔案保存在記憶體中，關機後就會消失。開啟 **Files** 應用程式，在側邊欄中點選你的第二支 USB 隨身碟或資料磁碟，將其掛載。接著查看它掛載在哪裡：

```sh
lsblk -o NAME,SIZE,FSTYPE,LABEL,MOUNTPOINTS
```

把檔案複製到那個資料夾（`MOUNTPOINTS` 欄中的路徑，例如 `/media/mint/Data`）：

```sh
cp ~/bios1.bin ~/flashrom.txt /media/mint/Data/
sync
```

## 7. 恢復原狀

1. 重新啟動，拔下隨身碟，按 **F2** 再次開啟 BIOS 設定。
2. 在 **開機**（Boot）索引標籤中，將 **安全開機**（Secure Boot）改回 **啟用**（Enabled）。
3. 在 **安全性**（Security）索引標籤中選擇 **設定監督員密碼**（Set Supervisor Password），輸入目前的密碼，新密碼留白，這樣就會移除密碼。
4. 按 **F10** 儲存並重新啟動進入 Windows。

如果你暫停了 BitLocker，請在以系統管理員身分執行的終端機中重新開啟它：

```powershell
manage-bde -protectors -enable C:
```

## 8. 寄送傾印檔

只需要 `bios1.bin`（如果讀取失敗，再加上 `flashrom.txt`）。[建立 issue](https://github.com/archivesteak/opensense/issues)，寫下你的筆電型號與 BIOS 版本（OpenSense 在 **設定 → 您的筆記型電腦** 中顯示這兩項），並附上傾印檔。GitHub 只接受 25 MB 以內的部分類型檔案：如果傾印檔更大或被 GitHub 拒絕，請先把它壓縮成 `.zip` 壓縮檔。如果壓縮檔仍然太大，請上傳到任一檔案分享網站並貼上連結。

> [!WARNING]
> issue 是公開的，而傾印檔包含你的筆電的資訊：序號、Acer 存放在韌體中的 Windows 授權金鑰，以及你在第 2 步設定的監督員密碼（所以它應該是你在其他地方都不用的密碼）。如果你不想公開這些資訊，請改用電子郵件把 zip 檔（或其連結）寄到 [archivesteak@gmail.com](mailto:archivesteak@gmail.com)，並附上筆電型號與 BIOS 版本。
