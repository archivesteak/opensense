# Dizüstü bilgisayarınızın bellenimini dökme

[English](dump-firmware.md) |
[Bahasa Indonesia](dump-firmware-id.md) |
[Deutsch](dump-firmware-de.md) |
[Español](dump-firmware-es.md) |
[Français](dump-firmware-fr.md) |
[Polski](dump-firmware-pl.md) |
[Português (Brasil)](dump-firmware-pt-BR.md) |
[Tiếng Việt](dump-firmware-vi.md) |
**Türkçe** |
[Русский](dump-firmware-ru.md) |
[Українська](dump-firmware-uk.md) |
[简体中文](dump-firmware-zh-CN.md) |
[繁體中文](dump-firmware-zh-TW.md)

Dizüstünüzde bir özellik eksikse ya da düzgün çalışmıyorsa, belleniminin bir kopyası bunun modelinizde gerçekte nasıl çalıştığını gösterir. OpenSense'in fanlar hakkında bildiklerinin çoğu, Nitro 5 AN515-57'nin kendi belleniminden gelir: fan hızının yalnızca %10'luk adımlarla artırılabildiği ve bellenimdeki fan eğrisinin bu modelde hiçbir şey yapmadığı böyle anlaşıldı. Dökümü en çok işe yarayacak modellerin listesi [README](../README-tr.md#bellenim-dökümleri) içinde.

Bu kılavuzla dizüstünün BIOS flash yongasını canlı bir Linux USB belleğinden okuyacak ve içeriğini tek bir dosyaya kaydedeceksiniz. AN515-57'de bu dosyada, fanları ve klavye aydınlatmasını yöneten gömülü denetleyicinin (EC) bellenimi de vardı. **Dizüstüne hiçbir şey yazılmaz.** Yaklaşık yarım saat sürer.

## Gerekenler

- Linux için 4 GB veya daha büyük bir USB bellek (içindekiler silinir).
- Dökümü kaydedecek bir yer: ikinci bir USB bellek ya da dizüstünde Windows sürücüsü olmayan bir bölüm, örneğin bir `D:` veri sürücüsü.
- Dizüstünün baştan sona prize takılı olması.

> [!IMPORTANT]
> **BitLocker.** Birçok dizüstü, Windows sürücüsü şifrelenmiş olarak gelir (*Cihaz şifrelemesi*). Secure Boot'u kapattığınızda Windows bir sonraki açılışta BitLocker kurtarma anahtarını ister. Başlamadan önce ya anahtarınızı bulun (Microsoft hesabınızda, [aka.ms/myrecoverykey](https://aka.ms/myrecoverykey) adresindedir) ya da BitLocker'ı yeniden açana kadar askıya alın. Bunun için yönetici olarak çalıştırılan bir terminale şunu yazın:
>
> ```powershell
> manage-bde -protectors -disable C: -RebootCount 0
> ```

## 1. Linux USB belleğini hazırlayın

**Linux Mint**'i (Cinnamon sürümü) [linuxmint.com](https://linuxmint.com/download.php) adresinden indirin ve [Rufus](https://rufus.ie) ile USB belleğe yazın. Varsayılan ayarları değiştirmeniz gerekmez.

## 2. Secure Boot'u kapatın

Secure Boot açıkken Linux, programların donanıma doğrudan erişmesine izin vermez; bu erişim olmadan da yonga okunamaz. BIOS'ta Türkçe yok, bu yüzden aşağıdaki seçenekler ekranda göründüğü gibi İngilizce yazılmıştır.

1. Dizüstünü yeniden başlatın ve BIOS ayarlarına girmek için Acer logosu görünürken **F2** tuşuna art arda basın.
2. Acer, **Secure Boot**'un ancak bir süpervizör parolası belirlendikten sonra değiştirilmesine izin verir. **Security** sekmesinde **Set Supervisor Password**'ü seçin ve bir parola belirleyin.
3. **Boot** sekmesinde **Secure Boot**'u **Disabled** yapın.
4. **Main** sekmesinde, zaten öyle değilse **F12 Boot Menu**'yü **Enabled** yapın.
5. Kaydedip yeniden başlatmak için **F10** tuşuna basın.

## 3. Linux'u USB bellekten başlatın

Belleği takın, yeniden başlatın ve Acer logosu görünürken **F12** tuşuna basın. USB belleği, ardından **Start Linux Mint**'i seçin. Linux doğrudan USB bellekten çalışır ve Windows'a dokunmaz.

## 4. flashrom'u kurun

Wi-Fi'ye ya da kabloyla bağlanın (sağ alttaki ağ simgesi), **Terminal**'i açın ve şunu yazın:

```sh
sudo apt update
sudo apt install -y flashrom
```

## 5. Yongayı okuyun

Önce flashrom yongayı bulsun. Bu komut henüz hiçbir şey okumaz:

```sh
sudo flashrom -p internal
```

Yonganın adını ve boyutunu gösterir, ardından bunun bir dizüstü olduğu uyarısıyla durur. Bu normaldir: flashrom, yanlışlıkla bir şey yazmamak için dizüstülerde varsayılan olarak çalışmayı reddeder; burada ise yalnızca okuma yapılır. Birden fazla uygun yonga bulursa aşağıdaki komutlara bu adlardan biriyle `-c "AD"` ekleyin.

Şimdi yongayı iki kez okuyun ve iki kopyayı karşılaştırın:

```sh
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios1.bin 2>&1 | tee ~/flashrom.txt
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios2.bin
cmp ~/bios1.bin ~/bios2.bin && echo "DUMP OK" || echo "DIFFERENT, read again"
```

Ekranda `DUMP OK` görünmelidir. Kopyalar farklıysa iki okuma komutunu tekrarlayın. Dosyada yalnızca `FF`'den oluşan büyük bölümlerin olması (Intel ME bölgesi) normaldir.

flashrom yongayı hiç okuyamıyorsa (bazı AMD dizüstülerde bu olabilir), `flashrom.txt` dosyasını saklayın: içindeki hata iletisi de işe yarar.

## 6. Dökümü kaydedin

Canlı sistem dosyaları RAM'de tutar, bu yüzden bilgisayar kapanınca dosyalar kaybolur. **Files** uygulamasını açın ve kenar çubuğunda ikinci USB belleğinize veya veri sürücünüze tıklayarak onu bağlayın. Ardından nereye bağlandığına bakın:

```sh
lsblk -o NAME,SIZE,FSTYPE,LABEL,MOUNTPOINTS
```

Dosyaları o klasöre kopyalayın (`MOUNTPOINTS` sütunundaki yol, örneğin `/media/mint/Data`):

```sh
cp ~/bios1.bin ~/flashrom.txt /media/mint/Data/
sync
```

## 7. Her şeyi eski haline getirin

1. Yeniden başlatın, USB belleği çıkarın ve BIOS kurulumunu yeniden açmak için **F2** tuşuna basın.
2. **Boot** sekmesinde **Secure Boot**'u yeniden **Enabled** yapın.
3. **Security** sekmesinde **Set Supervisor Password**'ü seçin, mevcut parolayı girin ve yeni parola alanını boş bırakın; böylece parola kaldırılır.
4. Kaydedip Windows'u başlatmak için **F10** tuşuna basın.

BitLocker'ı askıya aldıysanız yeniden açın. Bunun için yönetici olarak çalıştırılan bir terminale şunu yazın:

```powershell
manage-bde -protectors -enable C:
```

## 8. Dökümü gönderin

Yalnızca `bios1.bin` gerekir (okuma başarısız olduysa `flashrom.txt` de). [Bir issue açın](https://github.com/archivesteak/opensense/issues), dizüstünüzün modelini ve BIOS sürümünü yazın (OpenSense ikisini de **Ayarlar → Dizüstü bilgisayarınız** altında gösterir) ve dökümü ekleyin. GitHub 25 MB'a kadar ve yalnızca bazı türlerdeki dosyaları kabul eder: döküm daha büyükse ya da GitHub onu reddederse, önce bir `.zip` arşivine sıkıştırın. Arşiv hâlâ çok büyükse herhangi bir dosya paylaşım hizmetine yükleyip bağlantısını yapıştırın.

> [!WARNING]
> Issue'lar herkese açıktır ve döküm dizüstünüzle ilgili bilgiler içerir: seri numarası, Acer'ın bellenime kaydettiği Windows lisans anahtarı ve 2. adımda belirlediğiniz süpervizör parolası (bu yüzden başka hiçbir yerde kullanmadığınız bir parola olmalıdır). Bunları yayımlamak istemezseniz zip dosyasını (ya da bağlantısını) dizüstünüzün modeli ve BIOS sürümüyle birlikte [archivesteak@gmail.com](mailto:archivesteak@gmail.com) adresine e-postayla gönderin.
