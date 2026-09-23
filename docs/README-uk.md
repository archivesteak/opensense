<div align="center">

![OpenSense](../resources/banner.png)

**Керування вентиляторами, продуктивністю та підсвічуванням для ноутбуків Acer Nitro і Predator.**<br>
Заміна NitroSense з відкритим кодом.

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&color=E4473C)](../LICENSE)

[**Завантажити**](https://github.com/archivesteak/opensense/releases/latest) •
[Можливості](#можливості) •
[Підтримувані ноутбуки](#підтримувані-ноутбуки) •
[Збирання](#збирання)

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
**Українська** |
[简体中文](README-zh-CN.md) |
[繁體中文](README-zh-TW.md)

Допоможіть перекласти OpenSense і цю сторінку своєю мовою: див. розділ [Переклади](#переклади).

</div>

## Можливості

- **Справжні температури.** Температури ЦП і ГП зчитуються з самих чипів, як це роблять HWiNFO і ThrottleStop, із графіками за 5 хвилин.
- **Керування вентиляторами.** «Авто», «Макс.», фіксована швидкість для кожного вентилятора або температурні криві, які ви малюєте самі.
- **Безпека за замовчуванням.** За аварійної температури вентилятори переходять на повну швидкість, а якщо датчик або OpenSense перестає працювати, керування знову перебирає мікропрограма.
- **Продуктивність.** Режими роботи, CoolBoost і схеми живлення Windows.
- **Підсвічування.** Статичні кольори для кожної зони клавіатури або ефекти «Дихання», «Неон», «Хвиля», «Зсув» і «Масштаб».
- **Клавіатура й дисплей.** Автовимкнення підсвічування, блокування клавіші Windows, LCD overdrive і перемикач ГП (MUX).
- **Без запитів прав адміністратора.** Невелика фонова служба застосовує ваші налаштування від самого запуску, а клавіша NitroSense відкриває застосунок.
- **Ваша мова.** 36 мов: як у Windows або на вибір у налаштуваннях.

OpenSense запитує в мікропрограми, що є у вашому ноутбуці, і показує лише те, що він підтримує.

## Установлення

Завантажте останню версію в розділі [**Releases**](https://github.com/archivesteak/opensense/releases/latest) (Windows 10 2004 або новіша, 64-розрядна):

- **`OpenSense-<version>-Setup-x64.exe`**: інсталятор. Він містить усе, що потрібно OpenSense, і програма сама оновлюється.
- **`OpenSense-<version>-Portable-x64.zip`**: без установлення. Запитує права адміністратора й керує ноутбуком, лише поки відкрита.
  Драйвера PawnIO в ній немає, тож доки ви не встановите його окремо зі [сторінки випусків PawnIO](https://github.com/namazso/PawnIO.Setup/releases/latest), температура ЦП береться з мікропрограми ноутбука, а вона менш точна.

> [!NOTE]
> Перед використанням OpenSense вимкніть NitroSense (або видаліть його), інакше програми боротимуться за вентилятори.

## Підтримувані ноутбуки

Ноутбуки Acer з ігровим інтерфейсом мікропрограми, яким користуються NitroSense і PredatorSense. OpenSense розробляється на **Nitro 5 AN515-57**.

Спробували на іншій моделі? [Створіть issue](https://github.com/archivesteak/opensense/issues) і вставте діагностичні дані з **Настройки → Усунення неполадок → Копіювати**.

## Переклади

OpenSense використовує мову інтерфейсу Windows або мову, вибрану в **Настройки → Вигляд → Мова**. Переклади, зокрема цю сторінку, ще не перевіряв носій мови, тож виправлення вітаються.

Тексти застосунку містяться в `src/OpenSense.App/Strings/<language>/Resources.resw`, тексти інсталятора — в `installer/Strings/<language>.nsh`, з поясненням до кожного рядка в англійських файлах; `dotnet test` перевіряє, що в кожному перекладі є всі рядки й підстановки. Переклади цієї сторінки містяться в [`docs`](.).

## Збирання

Потрібен [.NET 10 SDK](https://dotnet.microsoft.com/download) під Windows.

```powershell
dotnet build
dotnet test --project tests/OpenSense.Core.Tests
```

Кожен push збирається, тестується й пакується в [GitHub Actions](../.github/workflows/build.yml); там само видно, як інсталятор створюється за допомогою [NSIS](https://nsis.sourceforge.io). Push тегу `v1.2.3` публікує випуск.

## Подяки

- [PawnIO](https://pawnio.eu): підписаний драйвер, який зчитує температури ЦП (його модулі поширюються за LGPL-2.1)
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK), [Win2D](https://github.com/microsoft/Win2D) і [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon), [StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc) і [Serilog](https://serilog.net)

## Ліцензія

OpenSense поширюється за ліцензією [GNU General Public License v3.0 або новішої версії](../LICENSE).

Це незалежний проєкт, написаний на основі аналізу сумісності, і він не містить коду Acer. Проєкт не пов’язаний з Acer і не схвалений нею. Acer, Nitro, Predator і NitroSense є торговельними марками Acer Inc.
