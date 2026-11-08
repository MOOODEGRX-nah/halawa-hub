# Halawa-Hub

**الإصدار: v0.0.10.63 (Beta)**

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

## لينكس

كل إصدار على [صفحة الإصدارات](https://github.com/MOOODEGRX-nah/halawa-hub/releases/latest) فيه:
`Halawa-Hub-linux-x64.tar.gz` و`Halawa-Hub-x86_64.AppImage` و`SHA256SUMS.txt` للتحقق منهما:

```bash
sha256sum --check --ignore-missing SHA256SUMS.txt
```

يحتاج x86_64 وX11 أو XWayland (كل أسطح مكتب Wayland الشائعة توفره).

### tar.gz (بدون root)

```bash
tar -xzf Halawa-Hub-linux-x64.tar.gz
./Halawa-Hub/install.sh
```

يثبّت في `~/.local/opt/halawa-hub`، ويظهر في قائمة التطبيقات بأيقونته، ويضيف الأمر `~/.local/bin/halawa-hub`.
إلغاء التثبيت: `~/.local/opt/halawa-hub/uninstall.sh`

### AppImage

```bash
chmod +x Halawa-Hub-x86_64.AppImage
./Halawa-Hub-x86_64.AppImage
```

ملف واحد بلا تثبيت (يحتاج FUSE). لإظهاره في قائمة التطبيقات: الإعدادات ثم «التشغيل» ثم «إظهار في قائمة التطبيقات».
إلغاء التثبيت: أوقف هذا الخيار وخيار التشغيل مع النظام، ثم احذف الملف.

### Arch وCachyOS (AUR)

ملف الحزمة `halawa-hub-bin` جاهز في [`packaging/aur`](packaging/aur). بعد نشرها على AUR يكون التثبيت:

```bash
paru -S halawa-hub-bin
```

### التحديث

- tar.gz وAppImage: زر «تثبيت الآن» في الإعدادات ينزّل الإصدار الجديد ويتحقق من SHA256 ويستبدل البرنامج ثم يعيد تشغيله.
- AUR أو حزم النظام أو مجلد للقراءة فقط: إشعار بالإصدار الجديد وزر يفتح صفحته، والتحديث عبر مدير الحزم.

بياناتك (المفضلة وساعات اللعب والأغلفة) في `~/.local/share/HalawaHub`: لا يلمسها التحديث ولا إلغاء التثبيت.

### أيقونة الصينية

تعمل على KDE Plasma وكل سطح مكتب فيه StatusNotifier. GNOME يحتاج إضافة
[AppIndicator](https://extensions.gnome.org/extension/615/appindicator-support/)؛ بدونها زر الإغلاق يغلق البرنامج فعليًا بدل إخفائه.


## أغلفة لمنصات غير Steam (اختياري)
Steam يجيب غلافه تلقائيًا بدون إعداد. لباقي المنصات (GOG, Epic, Riot, Xbox, Heroic, Lutris):

1. سجّل حساب مجاني بـ [SteamGridDB](https://www.steamgriddb.com/profile/preferences) وخذ مفتاح API من تبويب API
2. افتح البرنامج، ثم الإعدادات، ثم «مفاتيح API»
3. الصق المفتاح واضغط «تحقق»

ما يحتاج إعادة تشغيل: الأغلفة الناقصة تبدأ تظهر مباشرة بعد نجاح التحقق.
