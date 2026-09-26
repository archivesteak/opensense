# Copier le firmware de votre portable

[English](dump-firmware.md) |
[Bahasa Indonesia](dump-firmware-id.md) |
[Deutsch](dump-firmware-de.md) |
[Español](dump-firmware-es.md) |
**Français** |
[Polski](dump-firmware-pl.md) |
[Português (Brasil)](dump-firmware-pt-BR.md) |
[Tiếng Việt](dump-firmware-vi.md) |
[Türkçe](dump-firmware-tr.md) |
[Русский](dump-firmware-ru.md) |
[Українська](dump-firmware-uk.md) |
[简体中文](dump-firmware-zh-CN.md) |
[繁體中文](dump-firmware-zh-TW.md)

Si une fonction manque ou ne fonctionne pas bien sur votre portable, une copie de son firmware montre comment cela fonctionne réellement sur votre modèle. Une grande partie de ce qu’OpenSense sait des ventilateurs vient de la lecture du firmware du Nitro 5 AN515-57 : c’est ainsi qu’on a découvert que les ventilateurs ne sont accélérés que par paliers de 10 %, et que la courbe des ventilateurs du firmware ne fait rien sur ce modèle. La liste des modèles les plus utiles se trouve dans le [README](../README-fr.md#copies-du-firmware).

Avec ce guide, vous lirez la puce flash du BIOS du portable depuis une clé USB Linux live et enregistrerez son contenu dans un seul fichier. Sur l’AN515-57, ce fichier contenait aussi le firmware du contrôleur embarqué (EC), qui gère les ventilateurs et l’éclairage du clavier. **Rien n’est écrit sur le portable.** Comptez environ une demi-heure.

## Ce qu’il vous faut

- Une clé USB de 4 Go ou plus pour Linux (elle sera effacée).
- Un endroit où enregistrer la copie : une deuxième clé USB, ou une partition du portable qui n’est pas le lecteur Windows, comme un lecteur de données `D:`.
- Le portable branché sur secteur pendant tout ce temps.

> [!IMPORTANT]
> **BitLocker.** Beaucoup de portables sont livrés avec leur lecteur Windows chiffré (*Chiffrement de l’appareil*). Une fois Secure Boot désactivé, Windows demandera la clé de récupération BitLocker au démarrage suivant. Avant de commencer, retrouvez votre clé (elle se trouve dans votre compte Microsoft sur [aka.ms/myrecoverykey](https://aka.ms/myrecoverykey)) ou suspendez BitLocker jusqu’à ce que vous le réactiviez. Pour cela, tapez dans un terminal exécuté en tant qu’administrateur :
>
> ```powershell
> manage-bde -protectors -disable C: -RebootCount 0
> ```

## 1. Préparer la clé USB Linux

Téléchargez **Linux Mint** (l’édition Cinnamon) sur [linuxmint.com](https://linuxmint.com/download.php) et gravez-le sur la clé USB avec [Rufus](https://rufus.ie). Les réglages par défaut de Rufus conviennent.

## 2. Désactiver Secure Boot

Tant que Secure Boot est activé, Linux interdit aux programmes l’accès direct au matériel, sans lequel la puce ne peut pas être lue. Les noms ci-dessous sont ceux de la configuration du BIOS en français ; si la vôtre est en anglais, fiez-vous aux noms entre parenthèses.

1. Redémarrez le portable et appuyez plusieurs fois sur **F2** pendant que le logo Acer s’affiche, pour ouvrir la configuration du BIOS.
2. Acer ne laisse modifier le **Démarrage sécurisé** (Secure Boot) qu’une fois un mot de passe superviseur défini. Dans l’onglet **Sécurité** (Security), choisissez **Définir mot de passe superviseur** (Set Supervisor Password) et définissez-en un.
3. Dans l’onglet **Démarrage** (Boot), réglez **Démarrage sécurisé** (Secure Boot) sur **Désactivé** (Disabled).
4. Dans l’onglet **Principal** (Main), réglez **F12 Menu de démarrage** (F12 Boot Menu) sur **Activé** (Enabled) si ce n’est pas déjà le cas.
5. Appuyez sur **F10** pour enregistrer et redémarrer.

## 3. Démarrer Linux depuis la clé USB

Branchez la clé, redémarrez et appuyez sur **F12** pendant que le logo Acer s’affiche. Choisissez la clé USB, puis **Start Linux Mint**. Linux s’exécute directement depuis la clé et ne touche pas à Windows.

## 4. Installer flashrom

Connectez-vous au Wi-Fi ou par câble (l’icône réseau, en bas à droite), ouvrez **Terminal** et tapez :

```sh
sudo apt update
sudo apt install -y flashrom
```

## 5. Lire la puce

Laissez d’abord flashrom trouver la puce. Cette commande ne lit encore rien :

```sh
sudo flashrom -p internal
```

Elle affiche le nom et la taille de la puce, puis s’arrête sur un avertissement indiquant qu’il s’agit d’un portable. C’est normal : par défaut, flashrom refuse de fonctionner sur les portables pour ne rien écrire par accident, et ici on ne fait que lire. S’il trouve plusieurs puces possibles, ajoutez aux commandes ci-dessous `-c "NOM"` avec l’un de ces noms.

Lisez maintenant la puce deux fois et comparez les deux copies :

```sh
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios1.bin 2>&1 | tee ~/flashrom.txt
sudo flashrom -p internal:laptop=this_is_not_a_laptop -r ~/bios2.bin
cmp ~/bios1.bin ~/bios2.bin && echo "DUMP OK" || echo "DIFFERENT, read again"
```

Vous devez obtenir `DUMP OK`. Si les copies diffèrent, relancez les deux lectures. Les parties du fichier qui ne contiennent que des `FF` (la région Intel ME) sont normales.

Si flashrom n’arrive pas du tout à lire la puce (cela peut arriver sur certains portables AMD), gardez `flashrom.txt` : le message d’erreur qu’il contient est utile aussi.

## 6. Enregistrer la copie

Le système live garde ses fichiers en mémoire vive : ils disparaissent à l’extinction. Ouvrez l’application **Files** et cliquez dans la barre latérale sur votre deuxième clé USB ou votre lecteur de données pour le monter. Regardez ensuite où il a été monté :

```sh
lsblk -o NAME,SIZE,FSTYPE,LABEL,MOUNTPOINTS
```

Copiez les fichiers dans ce dossier (le chemin de la colonne `MOUNTPOINTS`, par exemple `/media/mint/Data`) :

```sh
cp ~/bios1.bin ~/flashrom.txt /media/mint/Data/
sync
```

## 7. Tout remettre en place

1. Redémarrez, retirez la clé USB et appuyez sur **F2** pour rouvrir la configuration du BIOS.
2. Dans l’onglet **Démarrage** (Boot), remettez **Démarrage sécurisé** (Secure Boot) sur **Activé** (Enabled).
3. Dans l’onglet **Sécurité** (Security), choisissez **Définir mot de passe superviseur** (Set Supervisor Password), saisissez le mot de passe actuel et laissez le nouveau vide pour le supprimer.
4. Appuyez sur **F10** pour enregistrer et redémarrer sous Windows.

Si vous avez suspendu BitLocker, réactivez-le dans un terminal exécuté en tant qu’administrateur :

```powershell
manage-bde -protectors -enable C:
```

## 8. Envoyer la copie

Seul `bios1.bin` est nécessaire (et `flashrom.txt` si la lecture a échoué). [Ouvrez une issue](https://github.com/archivesteak/opensense/issues), indiquez le modèle et la version du BIOS de votre portable (OpenSense les affiche dans **Paramètres → Votre ordinateur portable**) et joignez la copie. GitHub accepte les fichiers jusqu’à 25 Mo, et seulement certains types : si la copie est plus grosse ou que GitHub la refuse, compressez-la d’abord dans une archive `.zip`. Si l’archive est encore trop grosse, déposez-la sur un service d’hébergement de fichiers et collez le lien.

> [!WARNING]
> Les issues sont publiques, et la copie contient des informations sur votre portable : son numéro de série, la clé de licence Windows qu’Acer a enregistrée dans le firmware, et le mot de passe superviseur défini à l’étape 2 (c’est pourquoi il vaut mieux qu’il ne serve nulle part ailleurs). Si vous préférez ne pas les publier, envoyez plutôt le zip (ou un lien vers celui-ci) par e-mail à [archivesteak@gmail.com](mailto:archivesteak@gmail.com), avec le modèle et la version du BIOS de votre portable.
