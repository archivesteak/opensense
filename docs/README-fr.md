<div align="center">

![OpenSense](../resources/banner.png)

**Contrôle des ventilateurs, des performances et de l’éclairage pour les ordinateurs portables Acer Nitro et Predator.**<br>
Une alternative open source à NitroSense et PredatorSense.

[![Build](https://github.com/archivesteak/opensense/actions/workflows/build.yml/badge.svg)](https://github.com/archivesteak/opensense/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/archivesteak/opensense?label=Release&color=E4473C)](https://github.com/archivesteak/opensense/releases/latest)
[![License](https://img.shields.io/github/license/archivesteak/opensense?label=License&logo=gnu&color=C4282D)](../LICENSE)

[**Télécharger**](https://github.com/archivesteak/opensense/releases/latest) •
[Fonctionnalités](#fonctionnalités) •
[Ordinateurs pris en charge](#ordinateurs-pris-en-charge) •
[Compilation](#compilation)

[English](../README.md) |
[Bahasa Indonesia](README-id.md) |
[Deutsch](README-de.md) |
[Español](README-es.md) |
**Français** |
[Polski](README-pl.md) |
[Português (Brasil)](README-pt-BR.md) |
[Tiếng Việt](README-vi.md) |
[Türkçe](README-tr.md) |
[Русский](README-ru.md) |
[Українська](README-uk.md) |
[简体中文](README-zh-CN.md) |
[繁體中文](README-zh-TW.md)

Aidez à traduire OpenSense et cette page dans votre langue : voir [Traductions](#traductions).

</div>

## Fonctionnalités

- **De vraies températures.** Le processeur et le GPU sont lus directement sur les puces, comme le font HWiNFO et ThrottleStop, avec des graphiques sur 5 minutes.
- **Contrôle des ventilateurs.** Auto, qu’OpenSense accélère quand le portable chauffe (désactivable), Max, ou Manuel : de la vitesse ajoutée à chaque ventilateur, fixe ou suivant une courbe que vous tracez vous-même. Sur les portables qui en disposent, la courbe des ventilateurs du firmware peut être plus rapide, et DustDefender chasse la poussière.
- **Sûr par défaut.** Anti-bridage pousse les ventilateurs jusqu’à pleine vitesse quand le processeur approche de son seuil de bridage, et le firmware reprend la main si un capteur ou OpenSense s’arrête.
- **Performances.** Modes de fonctionnement, CoolBoost, overclocking du GPU pour chaque mode et modes de gestion de l’alimentation Windows. La touche de mode passe d’un mode à l’autre, et la batterie a son propre mode. Sur les Predator de 2024 et après, l’overclocking du GPU qu’Acer prévoit pour chaque mode s’y ajoute.
- **Éclairage.** Des couleurs fixes par zone du clavier, ou les effets Respiration, Néon, Vague, Défilement, Zoom, Météore et Scintillement. Aussi les barres lumineuses, l’Infinity Mirror, l’InfiniteRing, le logo et les touches Turbo et Mode, chacun avec ses propres effets. Les claviers éclairés touche par touche et les touches MagForce prennent une couleur par touche et ont leurs propres effets.
- **Clavier et écran.** Extinction auto du rétroéclairage, verrouillage de la touche Windows, overdrive LCD et le commutateur de GPU (MUX).
- **Batterie.** État de la batterie (capacité restante et cycles de charge), arrêt de la charge à 80 %, calibrage de la batterie et charge des appareils USB quand l’ordinateur portable est éteint.
- **Démarrage.** L’animation et le son de démarrage, et votre propre logo de démarrage sur les ordinateurs portables qui le permettent.
- **Aucune demande d’administrateur.** Un petit service en arrière-plan applique vos réglages dès le démarrage, et la touche NitroSense ouvre l’application.
- **Votre langue.** 36 langues, selon Windows ou au choix dans les paramètres.

OpenSense demande au firmware ce que possède votre ordinateur et n’affiche que ce qu’il prend en charge.

## Installation

Téléchargez la dernière version depuis [**Releases**](https://github.com/archivesteak/opensense/releases/latest) (Windows 10 2004 ou ultérieur, 64 bits) :

- **`OpenSense-<version>-Setup-x64.exe`** : le programme d’installation. Il contient tout ce dont OpenSense a besoin et se met à jour tout seul.
- **`OpenSense-<version>-Portable-x64.zip`** : sans installation. Demande les droits d’administrateur et ne contrôle l’ordinateur que tant qu’il est ouvert.
  Le pilote PawnIO n’est pas inclus : tant que vous ne l’installez pas séparément depuis [les versions de PawnIO](https://github.com/namazso/PawnIO.Setup/releases/latest), la température du processeur vient du firmware de l’ordinateur, qui est moins précise.

> [!NOTE]
> Désactivez NitroSense (ou désinstallez-le) avant d’utiliser OpenSense, sinon les deux se disputeront les ventilateurs.

## Ordinateurs pris en charge

Les ordinateurs portables Acer dotés de l’interface firmware de jeu qu’utilisent NitroSense et PredatorSense.

OpenSense a été développé sur un **Nitro 5 AN515-57**.

Vous l’avez essayé sur un autre modèle ? [Ouvrez une issue](https://github.com/archivesteak/opensense/issues) et collez les diagnostics copiés depuis **Paramètres → Résolution des problèmes → Copier**.

### Copies du firmware

Quelque chose manque ou ne fonctionne pas bien sur votre portable ? Envoyez une copie de son firmware : elle sera analysée pour comprendre comment cela fonctionne sur votre modèle. C’est ainsi qu’on a compris le fonctionnement des ventilateurs de l’AN515-57 : son firmware a montré qu’ils ne sont accélérés que par paliers de 10 % et que la courbe des ventilateurs du firmware ne fait rien. [Ce guide](firmware/dump-firmware-fr.md) explique comment faire la copie depuis une clé USB Linux, sans rien modifier sur le portable.

La copie provient uniquement de la puce du firmware du portable : aucun de vos fichiers, aucun compte ni rien d’autre de Windows ne s’y trouve. Les seules informations personnelles qu’elle contient sont le numéro de série du portable et la clé de licence Windows qu’Acer a enregistrée dans le firmware ; si vous préférez ne pas les publier, le guide explique comment envoyer la copie en privé.

Les modèles les plus utiles :

- **Nitro AN515-46, AN515-47, AN515-58, AN517-42, AN517-43 et AN517-55** : les seuls modèles sur lesquels le logiciel d’Acer règle la **Courbe des ventilateurs**, donc les seuls où OpenSense l’affiche. Personne n’a encore vérifié ce qu’elle y change.
- **Predator Helios 16 et 18 de 2024 et 2025 (PH16-72, PH18-72, PH16-73, PH18-73) et Helios Neo 16 (PHN16-72)** : sur les Predator de 2024 et après, les modes de fonctionnement et l’overclocking du GPU d’Acer passent par l’interface HID du contrôleur embarqué, qu’OpenSense pilote en se fondant uniquement sur le logiciel d’Acer.
- **Predator Helios 16 et 18 de 2023 (PH16-71, PH18-71) et Helios 3D 15 (PH3D15-71)** : la barre lumineuse arrière, dont le contrôleur embarqué génère les effets.
- **Tout autre modèle** : OpenSense accélère les ventilateurs par paliers de 10 % sur tous les portables, parce que le contrôleur de l’AN515-57 ignore les valeurs intermédiaires. Une copie montre si le vôtre fait de même.

## Traductions

OpenSense utilise la langue d’affichage de Windows, ou celle choisie dans **Paramètres → Apparence → Langue**. Aucune traduction n’a encore été relue par un locuteur natif, y compris cette page : les corrections sont les bienvenues.

Les textes de l’application se trouvent dans `src/OpenSense.App/Strings/<language>/Resources.resw` et ceux du programme d’installation dans `installer/Strings/<language>.nsh`, avec une note sur chaque texte dans les fichiers anglais ; `dotnet test` vérifie que chaque traduction contient tous les textes et espaces réservés. Les traductions de cette page se trouvent dans [`docs`](.).

## Compilation

Il vous faut le [SDK .NET 10](https://dotnet.microsoft.com/download) sous Windows.

```powershell
dotnet build
dotnet test --project tests/OpenSense.Core.Tests
```

Chaque push est compilé, testé et empaqueté par [GitHub Actions](../.github/workflows/build.yml), qui montre aussi comment le programme d’installation est créé avec [NSIS](https://nsis.sourceforge.io). Pousser un tag `v1.2.3` publie une version.

## Remerciements

- [PawnIO](https://pawnio.eu) : le pilote signé qui lit les températures du processeur (ses modules sont sous LGPL-2.1)
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK), [Win2D](https://github.com/microsoft/Win2D) et le [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon), [StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc) et [Serilog](https://serilog.net)

## Licence

OpenSense est distribué sous la [GNU General Public License v3.0 ou ultérieure](../LICENSE).

C’est un projet indépendant, écrit à partir d’une analyse d’interopérabilité, qui ne contient aucun code d’Acer. Il n’est ni affilié à Acer ni approuvé par Acer. Acer, Nitro, Predator et NitroSense sont des marques commerciales d’Acer Inc.
