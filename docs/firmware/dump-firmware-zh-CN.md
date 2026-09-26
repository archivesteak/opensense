# 转储笔记本电脑的固件

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
**简体中文** |
[繁體中文](dump-firmware-zh-TW.md)

如果你的笔记本缺少某项功能，或者某项功能工作不正常，它的固件副本就能说明这在你的机型上实际是怎样实现的。OpenSense 对风扇的了解大多来自 Nitro 5 AN515-57 的固件：正是这样才发现风扇转速只能以 10% 为步长提高，而且固件自带的风扇曲线在这款机型上不起作用。最需要的机型列表见 [README](../README-zh-CN.md#固件转储)。

按照本指南，你将从 Linux live U 盘读取笔记本的 BIOS 闪存芯片，并把其中的内容保存为一个文件。在 AN515-57 上，这个文件还包含嵌入式控制器（EC）的固件，它负责控制风扇和键盘灯光。**不会向笔记本写入任何内容。** 整个过程大约需要半小时。

## 需要准备

- 一个 4 GB 或更大的 U 盘，用于 Linux（其中的内容会被清除）。
- 保存转储文件的地方：第二个 U 盘，或笔记本上不是 Windows 驱动器的分区，例如数据盘 `D:`。
- 整个过程中笔记本一直接通电源。

> [!IMPORTANT]
> **BitLocker。** 许多笔记本出厂时 Windows 驱动器就已加密（*设备加密*）。关闭 Secure Boot 后，Windows 会在下次启动时要求输入 BitLocker 恢复密钥。开始之前，请先找到你的密钥（在你的 Microsoft 账户中，地址是 [aka.ms/myrecoverykey](https://aka.ms/myrecoverykey)），或者在以管理员身份运行的终端中暂停 BitLocker，直到你重新开启它：
>
> ```powershell
> manage-bde -protectors -disable C: -RebootCount 0
> ```

## 1. 制作 Linux U 盘

从 [linuxmint.com](https://linuxmint.com/download.php) 下载 **Linux Mint**（Cinnamon 版），并用 [Rufus](https://rufus.ie) 把镜像写入 U 盘，默认设置无需更改。

## 2. 关闭 Secure Boot

Secure Boot 开启时，Linux 不允许程序直接访问硬件，而读取芯片离不开这种访问。BIOS 没有简体中文界面，因此下面的选项名保留英文，与屏幕上显示的一致。

1. 重启笔记本，在 Acer 徽标出现时反复按 **F2**，进入 BIOS 设置。
2. Acer 只有在设置了管理员（supervisor）密码后才允许更改 **Secure Boot**。在 **Security** 选项卡中选择 **Set Supervisor Password** 并设置密码。
3. 在 **Boot** 选项卡中，将 **Secure Boot** 设为 **Disabled**。
4. 在 **Main** 选项卡中，如果 **F12 Boot Menu** 还不是 **Enabled**，将其设为 **Enabled**。
5. 按 **F10** 保存并重启。

## 3. 从 U 盘启动 Linux

插入 U 盘，重启，并在 Acer 徽标出现时按 **F12**。选择 U 盘，然后选择 **Start Linux Mint**。Linux 直接从 U 盘运行，不会改动 Windows。

## 4. 安装 flashrom

连接 Wi-Fi 或网线（右下角的网络图标），打开 **Terminal** 并输入：

```sh
sudo apt update
sudo apt install -y flashrom
```

## 5. 读取芯片

先让 flashrom 检测芯片。这条命令还不会读取任何内容：

```sh
sudo flashrom -p internal
```

它会显示芯片的名称和容量，然后因为检测到这是笔记本而停下并给出警告。这是正常的：为了避免误写，flashrom 默认不在笔记本上运行，而这里只做读取。如果它检测到多个可能的芯片，请在下面的命令中加上 `-c "名称"`，填入其中一个名称。

现在读取芯片两次，并比较两份副本：

```sh
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios1.bin 2>&1 | tee ~/flashrom.txt
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios2.bin
cmp ~/bios1.bin ~/bios2.bin && echo "DUMP OK" || echo "DIFFERENT, read again"
```

应该会显示 `DUMP OK`。如果两份副本不同，请重新运行这两条读取命令。文件中有大段全是 `FF`（Intel ME 区域），这是正常的。

如果 flashrom 完全无法读取芯片（某些 AMD 笔记本可能出现这种情况），请保留 `flashrom.txt`：其中的错误信息也有用。

## 6. 保存转储文件

Live 系统把文件保存在内存中，关机后就会消失。打开 **Files** 应用，在侧边栏中点击你的第二个 U 盘或数据盘，将其挂载。然后查看它挂载在哪里：

```sh
lsblk -o NAME,SIZE,FSTYPE,LABEL,MOUNTPOINTS
```

把文件复制到那个文件夹（`MOUNTPOINTS` 列中的路径，例如 `/media/mint/Data`）：

```sh
cp ~/bios1.bin ~/flashrom.txt /media/mint/Data/
sync
```

## 7. 恢复原状

1. 重启，拔下 U 盘，按 **F2** 再次打开 BIOS 设置。
2. 在 **Boot** 选项卡中，将 **Secure Boot** 改回 **Enabled**。
3. 在 **Security** 选项卡中选择 **Set Supervisor Password**，输入当前密码，新密码留空，这样就会删除密码。
4. 按 **F10** 保存并重启进入 Windows。

如果你暂停了 BitLocker，请在以管理员身份运行的终端中重新开启它：

```powershell
manage-bde -protectors -enable C:
```

## 8. 发送转储文件

只需要 `bios1.bin`（如果读取失败，再加上 `flashrom.txt`）。[提交 issue](https://github.com/archivesteak/opensense/issues)，写明你的笔记本型号和 BIOS 版本（OpenSense 在 **设置 → 你的笔记本电脑** 中显示这两项），并附上转储文件。GitHub 只接受 25 MB 以内的部分类型文件：如果转储文件更大或被 GitHub 拒绝，请先把它压缩成 `.zip` 压缩包。如果压缩包仍然太大，请上传到任意文件分享网站并粘贴链接。

> [!WARNING]
> issue 是公开的，而转储文件包含你的笔记本的信息：序列号、Acer 存放在固件中的 Windows 许可证密钥，以及你在第 2 步中设置的管理员密码（所以它应该是你在其他地方都不用的密码）。如果你不想公开这些信息，请改为通过电子邮件把 zip 文件（或其链接）发送到 [archivesteak@gmail.com](mailto:archivesteak@gmail.com)，并附上笔记本型号和 BIOS 版本。
