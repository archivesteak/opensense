# Volcar el firmware de tu portátil

[English](dump-firmware.md) |
[Bahasa Indonesia](dump-firmware-id.md) |
[Deutsch](dump-firmware-de.md) |
**Español** |
[Français](dump-firmware-fr.md) |
[Polski](dump-firmware-pl.md) |
[Português (Brasil)](dump-firmware-pt-BR.md) |
[Tiếng Việt](dump-firmware-vi.md) |
[Türkçe](dump-firmware-tr.md) |
[Русский](dump-firmware-ru.md) |
[Українська](dump-firmware-uk.md) |
[简体中文](dump-firmware-zh-CN.md) |
[繁體中文](dump-firmware-zh-TW.md)

Si falta una función o no funciona bien en tu portátil, una copia de su firmware muestra cómo funciona realmente en tu modelo. Gran parte de lo que OpenSense sabe de los ventiladores sale del propio firmware del Nitro 5 AN515-57: así se descubrió que los ventiladores solo se aceleran en pasos del 10 % y que la curva de los ventiladores del firmware no hace nada en ese modelo. La lista de los modelos que más ayudarían está en el [README](../README-es.md#volcados-del-firmware).

Con esta guía leerás el chip flash de la BIOS del portátil desde una memoria USB con Linux en modo live y guardarás su contenido en un solo archivo. En el AN515-57, ese archivo también contenía el firmware del controlador integrado (EC), que controla los ventiladores y la iluminación del teclado. **No se escribe nada en el portátil.** Lleva una media hora.

## Qué necesitas

- Una memoria USB de 4 GB o más para Linux (se borrará).
- Un sitio donde guardar el volcado: una segunda memoria USB o una partición del portátil que no sea la unidad de Windows, como una unidad de datos `D:`.
- El portátil enchufado todo el tiempo.

> [!IMPORTANT]
> **BitLocker.** Muchos portátiles vienen con la unidad de Windows cifrada (*Cifrado de dispositivo*). Al desactivar Secure Boot, Windows pedirá la clave de recuperación de BitLocker en el siguiente inicio. Antes de empezar, busca tu clave (está en tu cuenta de Microsoft, en [aka.ms/myrecoverykey](https://aka.ms/myrecoverykey)) o suspende BitLocker hasta que lo vuelvas a activar. Para ello, escribe en un terminal ejecutado como administrador:
>
> ```powershell
> manage-bde -protectors -disable C: -RebootCount 0
> ```

## 1. Crear la memoria USB con Linux

Descarga **Linux Mint** (la edición Cinnamon) desde [linuxmint.com](https://linuxmint.com/download.php) y grábalo en la memoria USB con [Rufus](https://rufus.ie). No hace falta cambiar la configuración predeterminada.

## 2. Desactivar Secure Boot

Mientras Secure Boot está activado, Linux no deja que los programas accedan directamente al hardware, y sin eso no se puede leer el chip. Los nombres de abajo son los de la configuración de la BIOS en español; si la tuya aparece en inglés, guíate por los que van entre paréntesis.

1. Reinicia el portátil y, mientras se ve el logotipo de Acer, pulsa **F2** varias veces para entrar en la configuración de la BIOS.
2. Acer solo permite cambiar el **Arranque seguro** (Secure Boot) cuando hay una contraseña de supervisor. En la pestaña **Seguridad** (Security), elige **Establecer contraseña de supervisor** (Set Supervisor Password) y establece una.
3. En la pestaña **Arranque** (Boot), pon **Arranque seguro** (Secure Boot) en **Deshabilitado** (Disabled).
4. En la pestaña **Principal** (Main), pon **F12 Menú de arranque** (F12 Boot Menu) en **Habilitado** (Enabled) si no lo está ya.
5. Pulsa **F10** para guardar y reiniciar.

## 3. Arrancar Linux desde la memoria USB

Conecta la memoria, reinicia y pulsa **F12** mientras se ve el logotipo de Acer. Elige la memoria USB y luego **Start Linux Mint**. Linux se ejecuta directamente desde la memoria USB y no toca Windows.

## 4. Instalar flashrom

Conéctate a la wifi o por cable (el icono de red, abajo a la derecha), abre **Terminal** y escribe:

```sh
sudo apt update
sudo apt install -y flashrom
```

## 5. Leer el chip

Primero, deja que flashrom busque el chip. Este comando todavía no lee nada:

```sh
sudo flashrom -p internal
```

Muestra el nombre y el tamaño del chip y luego se detiene con un aviso de que es un portátil. Es normal: en los portátiles, flashrom se niega a funcionar por defecto para no escribir nada por accidente, y aquí solo se lee. Si encuentra varios chips posibles, añade a los comandos de abajo `-c "NOMBRE"` con uno de esos nombres.

Ahora lee el chip dos veces y compara las dos copias:

```sh
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios1.bin 2>&1 | tee ~/flashrom.txt
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios2.bin
cmp ~/bios1.bin ~/bios2.bin && echo "DUMP OK" || echo "DIFFERENT, read again"
```

Debería aparecer `DUMP OK`. Si las copias no coinciden, repite las dos lecturas. Es normal que haya zonas grandes llenas de `FF` (la región Intel ME).

Si flashrom no puede leer el chip en absoluto (puede pasar en algunos portátiles AMD), guarda `flashrom.txt`: el mensaje de error que contiene también es útil.

## 6. Guardar el volcado

El sistema live guarda sus archivos en la memoria RAM, así que desaparecen al apagar. Abre la aplicación **Files** y haz clic en la barra lateral en tu segunda memoria USB o en tu unidad de datos para montarla. Luego mira dónde se ha montado:

```sh
lsblk -o NAME,SIZE,FSTYPE,LABEL,MOUNTPOINTS
```

Copia los archivos a esa carpeta (la ruta de la columna `MOUNTPOINTS`, por ejemplo `/media/mint/Data`):

```sh
cp ~/bios1.bin ~/flashrom.txt /media/mint/Data/
sync
```

## 7. Dejarlo todo como estaba

1. Reinicia, quita la memoria USB y pulsa **F2** para volver a abrir la configuración de la BIOS.
2. En la pestaña **Arranque** (Boot), vuelve a poner **Arranque seguro** (Secure Boot) en **Habilitado** (Enabled).
3. En la pestaña **Seguridad** (Security), elige **Establecer contraseña de supervisor** (Set Supervisor Password), escribe la contraseña actual y deja vacío el campo de la nueva: así se quita.
4. Pulsa **F10** para guardar y reiniciar en Windows.

Si suspendiste BitLocker, vuelve a activarlo. Escribe en un terminal ejecutado como administrador:

```powershell
manage-bde -protectors -enable C:
```

## 8. Enviar el volcado

Solo hace falta `bios1.bin` (y `flashrom.txt` si la lectura falló). [Abre una issue](https://github.com/archivesteak/opensense/issues), escribe el modelo y la versión de BIOS de tu portátil (OpenSense muestra ambos en **Configuración → Tu portátil**) y adjunta el volcado. GitHub acepta archivos de hasta 25 MB y solo de algunos tipos: si el volcado es más grande o GitHub lo rechaza, comprímelo antes en un archivo `.zip`. Si el archivo comprimido sigue siendo demasiado grande, súbelo a cualquier servicio de alojamiento de archivos y pega el enlace.

> [!WARNING]
> Las issues son públicas, y el volcado contiene datos de tu portátil: su número de serie, la clave de licencia de Windows que Acer guardó en el firmware y la contraseña de supervisor que pusiste en el paso 2 (por eso debería ser una que no uses en ningún otro sitio). Si prefieres no publicarlos, envía el zip (o un enlace a él) por correo a [archivesteak@gmail.com](mailto:archivesteak@gmail.com), con el modelo y la versión de BIOS de tu portátil.
