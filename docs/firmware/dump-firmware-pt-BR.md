# Como copiar o firmware do seu notebook

[English](dump-firmware.md) |
[Bahasa Indonesia](dump-firmware-id.md) |
[Deutsch](dump-firmware-de.md) |
[Español](dump-firmware-es.md) |
[Français](dump-firmware-fr.md) |
[Polski](dump-firmware-pl.md) |
**Português (Brasil)** |
[Tiếng Việt](dump-firmware-vi.md) |
[Türkçe](dump-firmware-tr.md) |
[Русский](dump-firmware-ru.md) |
[Українська](dump-firmware-uk.md) |
[简体中文](dump-firmware-zh-CN.md) |
[繁體中文](dump-firmware-zh-TW.md)

Se falta algum recurso no seu notebook ou ele não funciona direito, uma cópia do firmware mostra como isso funciona de verdade no seu modelo. Boa parte do que o OpenSense sabe sobre as ventoinhas vem da leitura do próprio firmware do Nitro 5 AN515-57: foi assim que se descobriu que as ventoinhas só são aceleradas em passos de 10% e que a curva das ventoinhas do firmware não faz nada nesse modelo. Os modelos que mais ajudariam estão listados no [README](../README-pt-BR.md#cópias-do-firmware).

Com este guia, você vai ler o chip flash do BIOS do notebook a partir de um pendrive com Linux em modo live e salvar o conteúdo dele em um único arquivo. No AN515-57, esse arquivo também continha o firmware do controlador embarcado (EC), que controla as ventoinhas e a iluminação do teclado. **Nada é gravado no notebook.** Leva cerca de meia hora.

## O que você precisa

- Um pendrive de 4 GB ou mais para o Linux (ele será apagado).
- Um lugar para salvar a cópia: um segundo pendrive ou uma partição do notebook que não seja a unidade do Windows, como uma unidade de dados `D:`.
- O notebook ligado na tomada o tempo todo.

> [!IMPORTANT]
> **BitLocker.** Muitos notebooks vêm com a unidade do Windows criptografada (*Criptografia do dispositivo*). Depois que o Secure Boot for desativado, o Windows vai pedir a chave de recuperação do BitLocker na próxima inicialização. Antes de começar, encontre sua chave (ela está na sua conta Microsoft em [aka.ms/myrecoverykey](https://aka.ms/myrecoverykey)) ou suspenda o BitLocker até reativá-lo. Para isso, digite em um terminal executado como administrador:
>
> ```powershell
> manage-bde -protectors -disable C: -RebootCount 0
> ```

## 1. Crie o pendrive com Linux

Baixe o **Linux Mint** (a edição Cinnamon) em [linuxmint.com](https://linuxmint.com/download.php) e grave-o no pendrive com o [Rufus](https://rufus.ie). Não é preciso mudar as configurações padrão.

## 2. Desative o Secure Boot

Enquanto o Secure Boot está ativado, o Linux não deixa os programas acessarem o hardware diretamente, e sem isso não dá para ler o chip. Os nomes abaixo são os da configuração do BIOS em português; se a sua estiver em inglês, siga os nomes entre parênteses.

1. Reinicie o notebook e pressione **F2** várias vezes enquanto o logotipo da Acer aparece, para abrir a configuração do BIOS.
2. A Acer só deixa mudar a **Inicialização Segura** (Secure Boot) depois que uma senha de supervisor é definida. Na guia **Segurança** (Security), escolha **Definir Senha do Supervisor** (Set Supervisor Password) e defina uma.
3. Na guia **Inicialização** (Boot), defina **Inicialização Segura** (Secure Boot) como **Desabilitado** (Disabled).
4. Na guia **Principal** (Main), defina **Menu de Inicialização F12** (F12 Boot Menu) como **Habilitado** (Enabled), se ainda não estiver.
5. Pressione **F10** para salvar e reiniciar.

## 3. Inicie o Linux pelo pendrive

Conecte o pendrive, reinicie e pressione **F12** enquanto o logotipo da Acer aparece. Escolha o pendrive e depois **Start Linux Mint**. O Linux roda a partir do pendrive e não mexe no Windows.

## 4. Instale o flashrom

Conecte-se ao Wi-Fi ou à rede cabeada (o ícone de rede, no canto inferior direito), abra o **Terminal** e digite:

```sh
sudo apt update
sudo apt install -y flashrom
```

## 5. Leia o chip

Primeiro, deixe o flashrom procurar o chip. Este comando ainda não lê nada:

```sh
sudo flashrom -p internal
```

Ele mostra o nome e o tamanho do chip e depois para com um aviso de que se trata de um notebook. Isso é normal: por padrão, o flashrom se recusa a funcionar em notebooks para não gravar nada por acidente, e aqui só fazemos leitura. Se ele encontrar vários chips possíveis, acrescente aos comandos abaixo `-c "NOME"` com um desses nomes.

Agora leia o chip duas vezes e compare as duas cópias:

```sh
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios1.bin 2>&1 | tee ~/flashrom.txt
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios2.bin
cmp ~/bios1.bin ~/bios2.bin && echo "DUMP OK" || echo "DIFFERENT, read again"
```

Deve aparecer `DUMP OK`. Se as cópias forem diferentes, repita as duas leituras. É normal haver trechos grandes só com `FF` (a região Intel ME).

Se o flashrom não conseguir ler o chip de jeito nenhum (isso pode acontecer em alguns notebooks AMD), guarde o `flashrom.txt`: a mensagem de erro que está nele também é útil.

## 6. Salve a cópia

O sistema live guarda os arquivos na memória RAM, então eles somem quando o notebook é desligado. Abra o aplicativo **Files** e, na barra lateral, clique no segundo pendrive ou na unidade de dados para montá-lo. Depois veja onde ele foi montado:

```sh
lsblk -o NAME,SIZE,FSTYPE,LABEL,MOUNTPOINTS
```

Copie os arquivos para essa pasta (o caminho da coluna `MOUNTPOINTS`, por exemplo `/media/mint/Data`):

```sh
cp ~/bios1.bin ~/flashrom.txt /media/mint/Data/
sync
```

## 7. Deixe tudo como estava

1. Reinicie, tire o pendrive e pressione **F2** para abrir a configuração do BIOS de novo.
2. Na guia **Inicialização** (Boot), defina **Inicialização Segura** (Secure Boot) de volta como **Habilitado** (Enabled).
3. Na guia **Segurança** (Security), escolha **Definir Senha do Supervisor** (Set Supervisor Password), digite a senha atual e deixe a nova em branco para removê-la.
4. Pressione **F10** para salvar e reiniciar no Windows.

Se você suspendeu o BitLocker, reative-o em um terminal executado como administrador:

```powershell
manage-bde -protectors -enable C:
```

## 8. Envie a cópia

Só é preciso o `bios1.bin` (e o `flashrom.txt`, se a leitura falhou). [Abra uma issue](https://github.com/archivesteak/opensense/issues), escreva o modelo e a versão do BIOS do seu notebook (o OpenSense mostra os dois em **Configurações → Seu notebook**) e anexe a cópia. O GitHub aceita arquivos de até 25 MB, e só de alguns tipos: se a cópia for maior ou o GitHub a recusar, compacte-a antes em um arquivo `.zip`. Se o arquivo compactado ainda for grande demais, envie-o para qualquer serviço de hospedagem de arquivos e cole o link.

> [!WARNING]
> As issues são públicas, e a cópia contém dados do seu notebook: o número de série, a chave de licença do Windows que a Acer gravou no firmware e a senha de supervisor que você definiu no passo 2 (por isso ela deve ser uma que você não use em outro lugar). Se preferir não publicá-los, envie o zip (ou um link para ele) por e-mail para [archivesteak@gmail.com](mailto:archivesteak@gmail.com), com o modelo e a versão do BIOS do seu notebook.
