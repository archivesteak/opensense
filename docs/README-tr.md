<div align="center">

![OpenSense](../resources/banner.png)

**Acer Nitro ve Predator dizüstü bilgisayarlar için fan, performans ve aydınlatma kontrolü.**<br>
NitroSense ve PredatorSense'in açık kaynaklı alternatifi.

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&logo=gnu&color=C4282D)](../LICENSE)

[**İndir**](https://github.com/archivesteak/opensense/releases/latest) •
[Özellikler](#özellikler) •
[Desteklenen dizüstü bilgisayarlar](#desteklenen-dizüstü-bilgisayarlar) •
[Derleme](#derleme)

[English](../README.md) |
[Bahasa Indonesia](README-id.md) |
[Deutsch](README-de.md) |
[Español](README-es.md) |
[Français](README-fr.md) |
[Polski](README-pl.md) |
[Português (Brasil)](README-pt-BR.md) |
[Tiếng Việt](README-vi.md) |
**Türkçe** |
[Русский](README-ru.md) |
[Українська](README-uk.md) |
[简体中文](README-zh-CN.md) |
[繁體中文](README-zh-TW.md)

OpenSense'i ve bu sayfayı kendi dilinize çevirmemize yardım edin: [Çeviriler](#çeviriler) bölümüne bakın.

</div>

## Özellikler

- **Gerçek sıcaklıklar.** CPU ve GPU sıcaklıkları, HWiNFO ve ThrottleStop'un yaptığı gibi doğrudan yongaların kendisinden okunur; 5 dakikalık grafiklerle.
- **Fan kontrolü.** Dizüstü ısındığında OpenSense'in hızlandırdığı Otomatik (kapatılabilir), Maks. ya da Özel: her fana eklenen, sabit veya kendi çizdiğiniz bir eğriyi izleyen hız. Bu özelliklere sahip dizüstülerde bellenimin kendi fan eğrisi hızlandırılabilir ve DustDefender tozu dışarı üfler.
- **Varsayılan olarak güvenli.** Kısılma önleme, işlemci kısılma noktasına yaklaştıkça fanları tam hıza çıkarır; bir sensör ya da OpenSense durursa denetimi yeniden bellenim devralır.
- **Performans.** Çalışma modları, CoolBoost, her mod için GPU hız aşırtma ve Windows güç planları. Mod tuşu modlar arasında geçiş yapar; pil gücü için ayrı bir mod seçilir. 2024 ve sonrası Predator'larda Acer'ın her mod için belirlediği GPU hız aşırtma da eklenir.
- **Aydınlatma.** Klavye bölgesi başına sabit renkler ya da Nefes, Neon, Dalga, Kayma, Yakınlaştırma, Meteor ve Parıltı efektleri. Işık çubukları, Infinity Mirror, InfiniteRing, logo ve Turbo ile Mod tuşları da, her biri kendi efektleriyle. Tuş başına aydınlatmalı klavyeler ve MagForce tuşları her tuş için ayrı bir renk alır ve kendi efektlerine sahiptir.
- **Klavye ve ekran.** Arka ışığı otomatik kapatma, Windows tuşu kilidi, LCD overdrive ve GPU (MUX) anahtarı.
- **Pil.** Pil sağlığı (kalan kapasite ve şarj döngüleri), şarjı %80'de durdurma, pil kalibrasyonu ve dizüstü bilgisayar kapalıyken USB cihazlarını şarj etme.
- **Başlangıç.** Açılış animasyonu ve sesi ile destekleyen dizüstü bilgisayarlarda kendi açılış logonuz.
- **Yönetici istemi yok.** Küçük bir arka plan hizmeti ayarlarınızı sistem açılışından itibaren uygular ve NitroSense tuşu uygulamayı açar.
- **Sizin diliniz.** 36 dil; Windows'u izler veya ayarlardan seçilir.

OpenSense, dizüstü bilgisayarınızda neler olduğunu bellenime sorar ve yalnızca desteklenenleri gösterir.

## Kurulum

En son sürümü [**Releases**](https://github.com/archivesteak/opensense/releases/latest) sayfasından indirin (Windows 10 2004 veya üzeri, 64 bit):

- **`OpenSense-<version>-Setup-x64.exe`**: yükleyici. OpenSense'in ihtiyaç duyduğu her şeyi içerir ve kendini güncel tutar.
- **`OpenSense-<version>-Portable-x64.zip`**: kurulum gerektirmez. Yönetici hakları ister ve dizüstü bilgisayarı yalnızca açıkken denetler.
  PawnIO sürücüsünü içermez; bu yüzden onu [PawnIO sürümlerinden](https://github.com/namazso/PawnIO.Setup/releases/latest) ayrıca yükleyene kadar CPU sıcaklığı dizüstü bilgisayarın belleniminden gelir ve daha az kesindir.

> [!NOTE]
> OpenSense'i kullanmadan önce NitroSense'i kapatın (veya kaldırın), aksi hâlde ikisi fanlar için çekişir.

## Desteklenen dizüstü bilgisayarlar

NitroSense ve PredatorSense'in kullandığı oyun bellenim arabirimine sahip Acer dizüstü bilgisayarlar.

OpenSense bir **Nitro 5 AN515-57** üzerinde geliştirildi.

Başka bir modelde denediniz mi? [Bir issue açın](https://github.com/archivesteak/opensense/issues) ve **Ayarlar → Sorun giderme → Kopyala** ile alınan tanılama bilgilerini yapıştırın.

### Bellenim dökümleri

Dizüstünüzde bir şey eksik mi ya da düzgün çalışmıyor mu? Belleniminin bir kopyasını gönderin; bunun modelinizde nasıl çalıştığını anlamak için incelenecek. AN515-57'nin fanlarının nasıl çalıştığı da böyle anlaşıldı: bellenimi, fan hızının yalnızca %10'luk adımlarla artırılabildiğini ve bellenimdeki fan eğrisinin hiçbir şey yapmadığını gösterdi. [Bu kılavuz](firmware/dump-firmware-tr.md), dizüstünde hiçbir şeyi değiştirmeden bir Linux USB belleğinden kopyanın nasıl alınacağını anlatır.

Kopya yalnızca dizüstünün bellenim yongasından alınır: içinde dosyalarınız, hesaplarınız ya da Windows'tan herhangi bir şey yoktur. İçerdiği tek kişisel bilgiler, dizüstünün seri numarası ve Acer'ın bellenime kaydettiği Windows lisans anahtarıdır; bunları yayımlamak istemezseniz kopyayı nasıl özel olarak göndereceğinizi kılavuz anlatır.

En çok yardımcı olacak modeller:

- **Nitro AN515-46, AN515-47, AN515-58, AN517-42, AN517-43 ve AN517-55**: Acer'ın yazılımının **Fan eğrisi** ayarını yaptığı tek modeller; bu yüzden OpenSense onu yalnızca bunlarda gösterir. Bu modellerde neyi değiştirdiğini henüz kimse denetlemedi.
- **2024 ve 2025 Predator Helios 16 ve 18 (PH16-72, PH18-72, PH16-73, PH18-73) ile Helios Neo 16 (PHN16-72)**: 2024 ve sonrası Predator'larda çalışma modları ve Acer'ın GPU hız aşırtması, gömülü denetleyicinin HID arabiriminden geçer; OpenSense bunu yalnızca Acer'ın yazılımına dayanarak yönetir.
- **2023 Predator Helios 16 ve 18 (PH16-71, PH18-71) ile Helios 3D 15 (PH3D15-71)**: efektlerini gömülü denetleyicinin oluşturduğu arka ışık çubuğu.
- **Diğer tüm modeller**: AN515-57'nin denetleyicisi aradaki değerleri yok saydığı için OpenSense fanları her dizüstünde %10'luk adımlarla hızlandırır. Bir döküm, sizinkinin de aynısını yapıp yapmadığını gösterir.

## Çeviriler

OpenSense, Windows'un görüntüleme dilini veya **Ayarlar → Görünüm → Dil** bölümünde seçilen dili kullanır. Bu sayfa dahil çevirilerin hiçbiri henüz anadili konuşan biri tarafından gözden geçirilmedi, bu yüzden düzeltmeler memnuniyetle karşılanır.

Uygulamanın metinleri `src/OpenSense.App/Strings/<language>/Resources.resw`, yükleyicininkiler ise `installer/Strings/<language>.nsh` içindedir; İngilizce dosyalarda her metin için bir not vardır. `dotnet test`, her çevirinin tüm metinleri ve yer tutucuları içerdiğini denetler. Bu sayfanın çevirileri [`docs`](.) klasöründedir.

## Derleme

Windows'ta [.NET 10 SDK](https://dotnet.microsoft.com/download) gerekir.

```powershell
dotnet build
dotnet test --project tests/OpenSense.Core.Tests
```

Her push [GitHub Actions](../.github/workflows/build.yml) tarafından derlenir, test edilir ve paketlenir; yükleyicinin [NSIS](https://nsis.sourceforge.io) ile nasıl oluşturulduğu da orada görülür. `v1.2.3` gibi bir etiket göndermek yeni bir sürüm yayımlar.

## Teşekkürler

- [PawnIO](https://pawnio.eu): CPU sıcaklıklarını okuyan imzalı sürücü (modülleri LGPL-2.1 lisanslıdır)
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK), [Win2D](https://github.com/microsoft/Win2D) ve [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon), [StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc) ve [Serilog](https://serilog.net)

## Lisans

OpenSense, [GNU General Public License v3.0 veya sonraki sürümleri](../LICENSE) ile lisanslanmıştır.

Birlikte çalışabilirlik analizine dayanarak yazılmış bağımsız bir projedir ve Acer kodu içermez. Acer ile bağlantılı değildir ve Acer tarafından onaylanmamıştır. Acer, Nitro, Predator ve NitroSense, Acer Inc.'in ticari markalarıdır.
