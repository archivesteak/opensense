<div align="center">

![OpenSense](../resources/banner.png)

**Controle de ventoinhas, desempenho e iluminação para notebooks Acer Nitro e Predator.**<br>
Um substituto de código aberto para o NitroSense e o PredatorSense.

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&logo=gnu&color=C4282D)](../LICENSE)

[**Baixar**](https://github.com/archivesteak/opensense/releases/latest) •
[Recursos](#recursos) •
[Notebooks compatíveis](#notebooks-compatíveis) •
[Compilação](#compilação)

[English](../README.md) |
[Bahasa Indonesia](README-id.md) |
[Deutsch](README-de.md) |
[Español](README-es.md) |
[Français](README-fr.md) |
[Polski](README-pl.md) |
**Português (Brasil)** |
[Tiếng Việt](README-vi.md) |
[Türkçe](README-tr.md) |
[Русский](README-ru.md) |
[Українська](README-uk.md) |
[简体中文](README-zh-CN.md) |
[繁體中文](README-zh-TW.md)

Ajude a traduzir o OpenSense e esta página para o seu idioma: veja [Traduções](#traduções).

</div>

## Recursos

- **Temperaturas reais.** A CPU e a GPU são lidas dos próprios chips, como fazem o HWiNFO e o ThrottleStop, com gráficos de 5 minutos.
- **Controle das ventoinhas.** Auto, que o OpenSense acelera quando o notebook esquenta (dá para desligar), Máx. ou Manual: velocidade adicional para cada ventoinha, fixa ou seguindo uma curva que você mesmo desenha. Nos notebooks que têm esses recursos, a curva das ventoinhas do próprio firmware pode ficar mais rápida e o DustDefender tira a poeira.
- **Seguro por padrão.** O Anti-throttling leva as ventoinhas à velocidade máxima quando o processador se aproxima do ponto de throttling, e o firmware assume de novo se um sensor ou o OpenSense parar.
- **Desempenho.** Modos de operação, CoolBoost, overclock da GPU para cada modo e planos de energia do Windows. A tecla de modo alterna entre os modos, e a bateria tem um modo próprio. Nos Predator de 2024 em diante, o overclock da GPU que a Acer define para cada modo é somado.
- **Iluminação.** Cores estáticas por zona do teclado, ou os efeitos Respiração, Neon, Onda, Deslocamento, Zoom, Meteoro e Cintilação. Também as barras de luz, o Infinity Mirror, o InfiniteRing, o logotipo e as teclas Turbo e de modo, cada um com seus próprios efeitos. Teclados com iluminação por tecla e as teclas MagForce aceitam uma cor para cada tecla e têm efeitos próprios.
- **Teclado e tela.** Desligamento automático da luz de fundo, bloqueio da tecla Windows, overdrive da tela LCD e a chave de GPU (MUX).
- **Bateria.** Saúde da bateria (capacidade restante e ciclos de carga), parar de carregar em 80%, calibração da bateria e carregamento de dispositivos USB com o notebook desligado.
- **Inicialização.** A animação e o som de inicialização, e seu próprio logotipo de inicialização nos notebooks compatíveis.
- **Sem pedidos de administrador.** Um pequeno serviço em segundo plano aplica suas configurações desde a inicialização, e a tecla NitroSense abre o aplicativo.
- **Seu idioma.** 36 idiomas, seguindo o Windows ou escolhido nas configurações.

O OpenSense pergunta ao firmware o que o seu notebook tem e mostra apenas o que ele suporta.

## Instalação

Baixe a versão mais recente em [**Releases**](https://github.com/archivesteak/opensense/releases/latest) (Windows 10 2004 ou posterior, 64 bits):

- **`OpenSense-<version>-Setup-x64.exe`**: o instalador. Inclui tudo o que o OpenSense precisa e se mantém atualizado sozinho.
- **`OpenSense-<version>-Portable-x64.zip`**: sem instalação. Pede direitos de administrador e controla o notebook apenas enquanto está aberto.
  Não inclui o driver PawnIO, então, até você instalá-lo separadamente pelas [versões do PawnIO](https://github.com/namazso/PawnIO.Setup/releases/latest), a temperatura da CPU vem do firmware do notebook, que é menos exata.

> [!NOTE]
> Desative o NitroSense (ou desinstale-o) antes de usar o OpenSense; caso contrário, os dois vão disputar as ventoinhas.

## Notebooks compatíveis

Notebooks Acer com a interface de firmware para jogos que o NitroSense e o PredatorSense usam.

O OpenSense foi desenvolvido em um **Nitro 5 AN515-57**.

Testou em outro modelo? [Abra uma issue](https://github.com/archivesteak/opensense/issues) e cole os diagnósticos de **Configurações → Solução de problemas → Copiar**.

### Cópias do firmware

Falta algo ou algo não funciona direito no seu notebook? Envie uma cópia do firmware dele: ela será analisada para descobrir como isso funciona no seu modelo. Foi assim que se entendeu como funcionam as ventoinhas do AN515-57: o firmware dele mostrou que elas só são aceleradas em passos de 10% e que a curva das ventoinhas do firmware não faz nada. [Este guia](firmware/dump-firmware-pt-BR.md) explica como fazer a cópia a partir de um pendrive com Linux, sem mudar nada no notebook.

A cópia vem só do chip do firmware do notebook: nenhum dos seus arquivos, contas ou qualquer outra coisa do Windows está nela. Os únicos dados pessoais que ela contém são o número de série do notebook e a chave de licença do Windows que a Acer gravou no firmware. Se preferir não publicá-los, o guia explica como enviar a cópia de forma privada.

Os modelos que mais ajudariam:

- **Nitro AN515-46, AN515-47, AN515-58, AN517-42, AN517-43 e AN517-55**: os únicos modelos em que o software da Acer ajusta a **Curva das ventoinhas**, e por isso os únicos em que o OpenSense a mostra. Ninguém verificou ainda o que ela muda neles.
- **Predator Helios 16 e 18 de 2024 e 2025 (PH16-72, PH18-72, PH16-73, PH18-73) e Helios Neo 16 (PHN16-72)**: nos Predator de 2024 em diante, os modos de operação e o overclock da GPU da Acer passam pela interface HID do controlador embarcado, que o OpenSense controla com base apenas no software da Acer.
- **Predator Helios 16 e 18 de 2023 (PH16-71, PH18-71) e Helios 3D 15 (PH3D15-71)**: a barra de luz traseira, cujos efeitos o controlador embarcado gera.
- **Qualquer outro modelo**: o OpenSense acelera as ventoinhas em passos de 10% em todos os notebooks, porque o controlador do AN515-57 descarta os valores intermediários. Uma cópia mostra se o seu faz o mesmo.

## Traduções

O OpenSense usa o idioma de exibição do Windows ou o escolhido em **Configurações → Aparência → Idioma**. Nenhum falante nativo revisou as traduções ainda, incluindo esta página, então correções são bem-vindas.

O texto do aplicativo fica em `src/OpenSense.App/Strings/<language>/Resources.resw` e o do instalador em `installer/Strings/<language>.nsh`, com uma nota sobre cada texto nos arquivos em inglês; `dotnet test` verifica se cada tradução tem todos os textos e espaços reservados. As traduções desta página ficam em [`docs`](.).

## Compilação

Você precisa do [SDK do .NET 10](https://dotnet.microsoft.com/download) no Windows.

```powershell
dotnet build
dotnet test --project tests/OpenSense.Core.Tests
```

Cada push é compilado, testado e empacotado pelo [GitHub Actions](../.github/workflows/build.yml), que também mostra como o instalador é feito com o [NSIS](https://nsis.sourceforge.io). Enviar uma tag `v1.2.3` publica uma versão.

## Créditos

- [PawnIO](https://pawnio.eu): o driver assinado que lê as temperaturas da CPU (seus módulos são LGPL-2.1)
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK), [Win2D](https://github.com/microsoft/Win2D) e o [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon), [StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc) e [Serilog](https://serilog.net)

## Licença

O OpenSense é licenciado sob a [GNU General Public License v3.0 ou posterior](../LICENSE).

É um projeto independente, escrito a partir de análise de interoperabilidade, e não contém código da Acer. Não é afiliado nem endossado pela Acer. Acer, Nitro, Predator e NitroSense são marcas comerciais da Acer Inc.
