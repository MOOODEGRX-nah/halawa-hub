# حزمة AUR: halawa-hub-bin

جاهزة للنشر عندما يفتح AUR التسجيل من جديد. `PKGBUILD` و`.SRCINFO` هنا مضبوطان على v0.0.10.63 المنشور وبُنيا فعلًا بـ makepkg. لنشر إصدار أحدث:

```bash
bash packaging/aur/update-pkgbuild.sh <version>   # يكتب الإصدار والهاش الحقيقي
cd packaging/aur
makepkg -f                                        # تجربة البناء محليًا
makepkg --printsrcinfo > .SRCINFO
```

ثم انسخ `PKGBUILD` و`.SRCINFO` إلى مستودع `ssh://aur@aur.archlinux.org/halawa-hub-bin.git`.

البرنامج المثبت من هذه الحزمة لا يحدّث نفسه: يعرض إشعار الإصدار الجديد ويفتح صفحته،
والتحديث يتم عبر مدير الحزم.
