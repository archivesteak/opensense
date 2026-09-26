<div align="center">

![OpenSense](../resources/banner.png)

**Control de ventiladores, rendimiento e iluminación para portátiles Acer Nitro y Predator.**<br>
Una alternativa de código abierto a NitroSense y PredatorSense.

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&logo=gnu&color=C4282D)](../LICENSE)

[**Descargar**](https://github.com/archivesteak/opensense/releases/latest) •
[Características](#características) •
[Portátiles compatibles](#portátiles-compatibles) •
[Compilación](#compilación)

[English](../README.md) |
[Bahasa Indonesia](README-id.md) |
[Deutsch](README-de.md) |
**Español** |
[Français](README-fr.md) |
[Polski](README-pl.md) |
[Português (Brasil)](README-pt-BR.md) |
[Tiếng Việt](README-vi.md) |
[Türkçe](README-tr.md) |
[Русский](README-ru.md) |
[Українська](README-uk.md) |
[简体中文](README-zh-CN.md) |
[繁體中文](README-zh-TW.md)

Ayuda a traducir OpenSense y esta página a tu idioma: consulta [Traducciones](#traducciones).

</div>

## Características

- **Temperaturas reales.** La CPU y la GPU se leen de los propios chips, como hacen HWiNFO y ThrottleStop, con gráficos de 5 minutos.
- **Control de ventiladores.** Auto, que OpenSense acelera cuando el portátil se calienta (puedes desactivarlo), Máx. o Manual: velocidad añadida a cada ventilador, fija o siguiendo una curva que dibujas tú mismo. En los portátiles que los tienen, la curva de los ventiladores del propio firmware puede ser más rápida y DustDefender expulsa el polvo.
- **Seguro por defecto.** Antilimitación térmica sube los ventiladores hasta la máxima velocidad cuando el procesador se acerca a su límite térmico, y el firmware vuelve a tomar el control si un sensor u OpenSense deja de funcionar.
- **Rendimiento.** Modos de funcionamiento, CoolBoost, overclock de la GPU para cada modo y planes de energía de Windows. La tecla de modo cambia entre los modos, y con batería se usa un modo propio. En los Predator de 2024 en adelante se suma el overclock de la GPU que Acer fija para cada modo.
- **Iluminación.** Colores estáticos por zona del teclado, o los efectos Respiración, Neón, Ola, Desplazamiento, Zoom, Meteoro y Centelleo. También las barras de luz, el Infinity Mirror, el InfiniteRing, el logotipo y las teclas Turbo y de modo, cada uno con sus propios efectos. Los teclados con iluminación por tecla y las teclas MagForce admiten un color para cada tecla y efectos propios.
- **Teclado y pantalla.** Apagado automático de la retroiluminación, bloqueo de la tecla Windows, overdrive de la pantalla LCD y el conmutador de GPU (MUX).
- **Batería.** Estado de la batería (capacidad restante y ciclos de carga), carga solo hasta el 80 %, calibración de la batería y carga de dispositivos USB con el portátil apagado.
- **Arranque.** La animación y el sonido de arranque, y tu propio logotipo de arranque en los portátiles que lo admiten.
- **Sin avisos de administrador.** Un pequeño servicio en segundo plano aplica tu configuración desde el inicio, y la tecla NitroSense abre la aplicación.
- **Tu idioma.** 36 idiomas, según Windows o elegido en la configuración.

OpenSense pregunta al firmware qué tiene tu portátil y muestra solo lo que admite.

## Instalación

Descarga la última versión desde [**Releases**](https://github.com/archivesteak/opensense/releases/latest) (Windows 10 2004 o posterior, 64 bits):

- **`OpenSense-<version>-Setup-x64.exe`**: el instalador. Incluye todo lo que OpenSense necesita y se mantiene actualizado solo.
- **`OpenSense-<version>-Portable-x64.zip`**: sin instalación. Pide permisos de administrador y controla el portátil solo mientras está abierto.
  No incluye el controlador PawnIO, así que hasta que lo instales por separado desde [las versiones de PawnIO](https://github.com/namazso/PawnIO.Setup/releases/latest), la temperatura de la CPU procede del firmware del portátil, que es menos exacta.

> [!NOTE]
> Desactiva NitroSense (o desinstálalo) antes de usar OpenSense; si no, los dos se pelearán por los ventiladores.

## Portátiles compatibles

Portátiles Acer con la interfaz de firmware para juegos que usan NitroSense y PredatorSense.

OpenSense se desarrolló en un **Nitro 5 AN515-57**.

¿Lo has probado en otro modelo? [Abre una issue](https://github.com/archivesteak/opensense/issues) y pega los diagnósticos de **Configuración → Solución de problemas → Copiar**.

### Volcados del firmware

¿Falta algo o no funciona bien en tu portátil? Envía una copia de su firmware y se analizará para averiguar cómo funciona en tu modelo. Así se averiguó cómo funcionan los ventiladores del AN515-57: su firmware mostró que solo se aceleran en pasos del 10 % y que la curva de los ventiladores del firmware no hace nada. [Esta guía](firmware/dump-firmware-es.md) explica cómo hacer la copia desde una memoria USB con Linux sin cambiar nada en el portátil.

La copia sale solo del chip del firmware del portátil: no contiene ninguno de tus archivos, cuentas ni nada más de Windows. Los únicos datos personales que incluye son el número de serie del portátil y la clave de licencia de Windows que Acer guardó en el firmware. Si prefieres no publicarlos, la guía explica cómo enviar la copia en privado.

Los modelos que más ayudarían:

- **Nitro AN515-46, AN515-47, AN515-58, AN517-42, AN517-43 y AN517-55**: los únicos modelos en los que el software de Acer ajusta la **Curva de los ventiladores**, y por eso los únicos en los que OpenSense la muestra. Nadie ha comprobado todavía qué cambia en ellos.
- **Predator Helios 16 y 18 de 2024 y 2025 (PH16-72, PH18-72, PH16-73, PH18-73) y Helios Neo 16 (PHN16-72)**: en los Predator de 2024 en adelante, los modos de funcionamiento y el overclock de la GPU de Acer pasan por la interfaz HID del controlador integrado, que OpenSense maneja basándose solo en el software de Acer.
- **Predator Helios 16 y 18 de 2023 (PH16-71, PH18-71) y Helios 3D 15 (PH3D15-71)**: la barra de luz trasera, cuyos efectos genera el controlador integrado.
- **Cualquier otro modelo**: OpenSense acelera los ventiladores en pasos del 10 % en todos los portátiles, porque el controlador del AN515-57 descarta todo lo intermedio. Un volcado muestra si el tuyo hace lo mismo.

## Traducciones

OpenSense usa el idioma de visualización de Windows o el que elijas en **Configuración → Apariencia → Idioma**. Ningún hablante nativo ha revisado todavía las traducciones, incluida esta página, así que las correcciones son bienvenidas.

El texto de la aplicación está en `src/OpenSense.App/Strings/<language>/Resources.resw` y el del instalador en `installer/Strings/<language>.nsh`, con una nota sobre cada cadena en los archivos en inglés; `dotnet test` comprueba que cada traducción tenga todas las cadenas y marcadores de posición. Las traducciones de esta página están en [`docs`](.).

## Compilación

Necesitas el [SDK de .NET 10](https://dotnet.microsoft.com/download) en Windows.

```powershell
dotnet build
dotnet test --project tests/OpenSense.Core.Tests
```

Cada push se compila, se prueba y se empaqueta con [GitHub Actions](../.github/workflows/build.yml), que también muestra cómo se crea el instalador con [NSIS](https://nsis.sourceforge.io). Al subir una etiqueta `v1.2.3` se publica una versión.

## Créditos

- [PawnIO](https://pawnio.eu): el controlador firmado que lee las temperaturas de la CPU (sus módulos tienen licencia LGPL-2.1)
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK), [Win2D](https://github.com/microsoft/Win2D) y el [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon), [StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc) y [Serilog](https://serilog.net)

## Licencia

OpenSense se distribuye bajo la [GNU General Public License v3.0 o posterior](../LICENSE).

Es un proyecto independiente, escrito a partir de un análisis de interoperabilidad, y no contiene código de Acer. No está afiliado a Acer ni cuenta con su respaldo. Acer, Nitro, Predator y NitroSense son marcas comerciales de Acer Inc.
