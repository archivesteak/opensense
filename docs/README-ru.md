<div align="center">

![OpenSense](../resources/banner.png)

**Управление вентиляторами, производительностью и подсветкой для ноутбуков Acer Nitro и Predator.**<br>
Замена NitroSense с открытым исходным кодом.

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&color=E4473C)](../LICENSE)

[**Скачать**](https://github.com/archivesteak/opensense/releases/latest) •
[Возможности](#возможности) •
[Поддерживаемые ноутбуки](#поддерживаемые-ноутбуки) •
[Сборка](#сборка)

[English](../README.md) |
[Bahasa Indonesia](README-id.md) |
[Deutsch](README-de.md) |
[Español](README-es.md) |
[Français](README-fr.md) |
[Polski](README-pl.md) |
[Português (Brasil)](README-pt-BR.md) |
[Tiếng Việt](README-vi.md) |
[Türkçe](README-tr.md) |
**Русский** |
[Українська](README-uk.md) |
[简体中文](README-zh-CN.md) |
[繁體中文](README-zh-TW.md)

Помогите перевести OpenSense и эту страницу на свой язык: см. раздел [Переводы](#переводы).

</div>

## Возможности

- **Настоящие температуры.** Температуры ЦП и ГП считываются с самих чипов, как это делают HWiNFO и ThrottleStop, с графиками за 5 минут.
- **Управление вентиляторами.** «Авто», которое OpenSense ускоряет при нагреве (это можно отключить), «Макс.» или «Вручную»: добавочная скорость для каждого вентилятора, фиксированная или по кривой, которую вы рисуете сами.
- **Безопасность по умолчанию.** «Антитроттлинг» раскручивает вентиляторы до полной скорости, когда процессор подходит к порогу троттлинга, а если датчик или OpenSense перестаёт работать, управление снова берёт на себя прошивка.
- **Производительность.** Режимы работы, CoolBoost и схемы электропитания Windows.
- **Подсветка.** Статичные цвета для каждой зоны клавиатуры или эффекты «Дыхание», «Неон», «Волна», «Смещение» и «Масштаб».
- **Клавиатура и дисплей.** Автоотключение подсветки, блокировка клавиши Windows, LCD overdrive и переключатель ГП (MUX).
- **Без запросов прав администратора.** Небольшая фоновая служба применяет ваши настройки с момента запуска, а клавиша NitroSense открывает приложение.
- **Ваш язык.** 36 языков: как в Windows или на выбор в параметрах.

OpenSense спрашивает у прошивки, что есть в вашем ноутбуке, и показывает только то, что он поддерживает.

## Установка

Скачайте последнюю версию в разделе [**Releases**](https://github.com/archivesteak/opensense/releases/latest) (Windows 10 2004 или новее, 64-разрядная):

- **`OpenSense-<version>-Setup-x64.exe`**: установщик. В нём есть всё, что нужно OpenSense, и программа сама обновляется.
- **`OpenSense-<version>-Portable-x64.zip`**: без установки. Запрашивает права администратора и управляет ноутбуком, только пока открыта.
  Драйвер PawnIO в неё не входит, поэтому, пока вы не установите его отдельно со [страницы выпусков PawnIO](https://github.com/namazso/PawnIO.Setup/releases/latest), температура ЦП берётся из прошивки ноутбука, а она менее точна.

> [!NOTE]
> Перед использованием OpenSense отключите NitroSense (или удалите его), иначе программы будут бороться за вентиляторы.

## Поддерживаемые ноутбуки

Ноутбуки Acer с игровым интерфейсом прошивки, которым пользуются NitroSense и PredatorSense.

OpenSense разработан на **Nitro 5 AN515-57**.

Попробовали на другой модели? [Создайте issue](https://github.com/archivesteak/opensense/issues) и вставьте диагностические данные из **Параметры → Устранение неполадок → Копировать**.

## Переводы

OpenSense использует язык интерфейса Windows или язык, выбранный в **Параметры → Оформление → Язык**. Переводы, включая эту страницу, ещё не проверял носитель языка, поэтому исправления приветствуются.

Тексты приложения находятся в `src/OpenSense.App/Strings/<language>/Resources.resw`, тексты установщика — в `installer/Strings/<language>.nsh`, с пояснением к каждой строке в английских файлах; `dotnet test` проверяет, что в каждом переводе есть все строки и подстановки. Переводы этой страницы находятся в [`docs`](.).

## Сборка

Нужен [.NET 10 SDK](https://dotnet.microsoft.com/download) под Windows.

```powershell
dotnet build
dotnet test --project tests/OpenSense.Core.Tests
```

Каждый push собирается, тестируется и упаковывается в [GitHub Actions](../.github/workflows/build.yml); там же видно, как установщик создаётся с помощью [NSIS](https://nsis.sourceforge.io). Push тега `v1.2.3` публикует выпуск.

## Благодарности

- [PawnIO](https://pawnio.eu): подписанный драйвер, который считывает температуры ЦП (его модули распространяются по LGPL-2.1)
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK), [Win2D](https://github.com/microsoft/Win2D) и [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon), [StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc) и [Serilog](https://serilog.net)

## Лицензия

OpenSense распространяется по лицензии [GNU General Public License v3.0 или более поздней версии](../LICENSE).

Это независимый проект, написанный на основе анализа совместимости, и он не содержит кода Acer. Проект не связан с Acer и не одобрен ею. Acer, Nitro, Predator и NitroSense являются товарными знаками Acer Inc.
