# Halawa-Hub

**الإصدار: v0.0.10.65 (Beta)**

البرنامج يفحص نفسه عن تحديثات تلقائيًا عند التشغيل ويعرض لك رابط تحميل مباشر لو فيه إصدار أحدث.

لانشر ألعاب شخصي يجمع مكتبتك من عدة منصات في مكان واحد.

## المنصات المدعومة

| المنصة | ويندوز | لينكس |
|---|---|---|
| Steam (مع ألعاب Proton) | ✓ | ✓ (عادي وFlatpak وSnap) |
| Heroic (Epic وGOG وAmazon) | | ✓ (عادي وFlatpak) |
| Lutris | | ✓ (عادي وFlatpak) |
| GOG | ✓ | عبر Heroic |
| Epic Games | ✓ | عبر Heroic |
| Riot Games (League of Legends, VALORANT, Legends of Runeterra) | ✓ | |
| Xbox / Microsoft Store (كشف تقريبي) | ✓ | |

## تحميل البرنامج
من الرابط الموقع ادناه (https://mooodegrx-nah.github.io/halawa-hub-web/)

## التثبيت على لينكس

يحتاج x86_64 وX11 أو XWayland (كل أسطح مكتب Wayland الشائعة توفره). بياناتك (المفضلة وساعات اللعب والأغلفة) في `~/.local/share/HalawaHub`: لا يلمسها التحديث ولا الإزالة.

### Arch وCachyOS: مستودع pacman

تحقق أولًا أن بصمة المفتاح هي `0DF73C78FAC4C71A7D07E245039EDF0A36693503`، ثم:

```bash
curl -fsSL https://github.com/MOOODEGRX-nah/halawa-hub/releases/download/pacman-repo/halawa-hub-repo.asc | sudo pacman-key --add -
sudo pacman-key --lsign-key 0DF73C78FAC4C71A7D07E245039EDF0A36693503
sudo tee -a /etc/pacman.conf <<'CONF'

[halawa-hub]
SigLevel = Required DatabaseRequired
Server = https://github.com/MOOODEGRX-nah/halawa-hub/releases/download/pacman-repo
CONF
sudo pacman -Syu halawa-hub
```

### أي توزيعة: سطر واحد بدون sudo

```bash
curl -fsSL https://raw.githubusercontent.com/MOOODEGRX-nah/halawa-hub/main/packaging/linux/get.sh | bash
```

ينزّل آخر إصدار، ويتحقق من SHA256، ويثبّت في `~/.local/opt/halawa-hub`، ويضيف البرنامج إلى قائمة التطبيقات.

### AppImage

من [صفحة الإصدارات](https://github.com/MOOODEGRX-nah/halawa-hub/releases/latest):

```bash
chmod +x Halawa-Hub-x86_64.AppImage
./Halawa-Hub-x86_64.AppImage
```

ملف واحد بلا تثبيت (يحتاج FUSE). لإظهاره في قائمة التطبيقات: الإعدادات ثم «التشغيل» ثم «إظهار في قائمة التطبيقات».

### tar.gz يدويًا

```bash
sha256sum --check --ignore-missing SHA256SUMS.txt
tar -xzf Halawa-Hub-linux-x64.tar.gz
./Halawa-Hub/install.sh
```

### التحديث

| طريقة التثبيت | التحديث |
|---|---|
| مستودع pacman | `sudo pacman -Syu` |
| سطر التثبيت، tar.gz، AppImage | زر «تثبيت الآن» في الإعدادات: ينزّل الإصدار ويتحقق منه ويعيد التشغيل |

### الإزالة

| طريقة التثبيت | الإزالة |
|---|---|
| مستودع pacman | `sudo pacman -R halawa-hub` |
| سطر التثبيت أو tar.gz | `curl -fsSL https://raw.githubusercontent.com/MOOODEGRX-nah/halawa-hub/main/packaging/linux/get.sh \| bash -s -- --uninstall` |
| AppImage | أوقف «إظهار في قائمة التطبيقات» و«تشغيل مع بدء النظام»، ثم احذف الملف |

لإزالة المستودع أيضًا: احذف قسم `[halawa-hub]` من `/etc/pacman.conf`، ثم `sudo pacman-key --delete 0DF73C78FAC4C71A7D07E245039EDF0A36693503`.

### AUR

قريبًا: حزمة `halawa-hub-bin` جاهزة في [`packaging/aur`](packaging/aur)، وتُنشر عند فتح التسجيل في AUR.

### أيقونة الصينية

تعمل على KDE Plasma وكل سطح مكتب فيه StatusNotifier. GNOME يحتاج إضافة [AppIndicator](https://extensions.gnome.org/extension/615/appindicator-support/)؛ بدونها زر الإغلاق يغلق البرنامج فعليًا بدل إخفائه.

## Installing on Linux

Requires x86_64 and X11 or XWayland (every common Wayland desktop provides it). Your data (favorites, play time, covers) lives in `~/.local/share/HalawaHub`: updating and removing never touch it.

### Arch and CachyOS: pacman repository

First check that the key fingerprint is `0DF73C78FAC4C71A7D07E245039EDF0A36693503`, then:

```bash
curl -fsSL https://github.com/MOOODEGRX-nah/halawa-hub/releases/download/pacman-repo/halawa-hub-repo.asc | sudo pacman-key --add -
sudo pacman-key --lsign-key 0DF73C78FAC4C71A7D07E245039EDF0A36693503
sudo tee -a /etc/pacman.conf <<'CONF'

[halawa-hub]
SigLevel = Required DatabaseRequired
Server = https://github.com/MOOODEGRX-nah/halawa-hub/releases/download/pacman-repo
CONF
sudo pacman -Syu halawa-hub
```

### Any distribution: one line, no sudo

```bash
curl -fsSL https://raw.githubusercontent.com/MOOODEGRX-nah/halawa-hub/main/packaging/linux/get.sh | bash
```

It downloads the latest release, verifies its SHA256, installs to `~/.local/opt/halawa-hub`, and adds the app to your applications menu.

### AppImage

From the [releases page](https://github.com/MOOODEGRX-nah/halawa-hub/releases/latest):

```bash
chmod +x Halawa-Hub-x86_64.AppImage
./Halawa-Hub-x86_64.AppImage
```

One file, no install (needs FUSE). To add it to the applications menu: Settings, then Startup, then "Show in the applications menu".

### tar.gz by hand

```bash
sha256sum --check --ignore-missing SHA256SUMS.txt
tar -xzf Halawa-Hub-linux-x64.tar.gz
./Halawa-Hub/install.sh
```

### Updating

| Installed with | How to update |
|---|---|
| pacman repository | `sudo pacman -Syu` |
| One-line installer, tar.gz, AppImage | "Install now" in Settings: downloads, verifies, and restarts |

### Removing

| Installed with | How to remove |
|---|---|
| pacman repository | `sudo pacman -R halawa-hub` |
| One-line installer or tar.gz | `curl -fsSL https://raw.githubusercontent.com/MOOODEGRX-nah/halawa-hub/main/packaging/linux/get.sh \| bash -s -- --uninstall` |
| AppImage | Turn off "Show in the applications menu" and "Start with system", then delete the file |

To remove the repository too: delete the `[halawa-hub]` section from `/etc/pacman.conf`, then run `sudo pacman-key --delete 0DF73C78FAC4C71A7D07E245039EDF0A36693503`.

### AUR

Coming soon: the `halawa-hub-bin` package is ready in [`packaging/aur`](packaging/aur) and will be published once AUR registration reopens.

### Tray icon

Works on KDE Plasma and any desktop with StatusNotifier. GNOME needs the [AppIndicator](https://extensions.gnome.org/extension/615/appindicator-support/) extension; without it the close button really exits instead of hiding the window.

## أغلفة لمنصات غير Steam (اختياري)
Steam يجيب غلافه تلقائيًا بدون إعداد. لباقي المنصات (GOG, Epic, Riot, Xbox, Heroic, Lutris):

1. سجّل حساب مجاني بـ [SteamGridDB](https://www.steamgriddb.com/profile/preferences) وخذ مفتاح API من تبويب API
2. افتح البرنامج، ثم الإعدادات، ثم «مفاتيح API»
3. الصق المفتاح واضغط «تحقق»

ما يحتاج إعادة تشغيل: الأغلفة الناقصة تبدأ تظهر مباشرة بعد نجاح التحقق.
