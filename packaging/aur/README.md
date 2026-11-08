# حزمة AUR: halawa-hub-bin

جاهزة للنشر من حساب AUR الخاص بك، بعد صدور إصدار فيه `Halawa-Hub-linux-x64.tar.gz`:

```bash
bash packaging/aur/update-pkgbuild.sh <version>   # يكتب الإصدار والهاش الحقيقي
cd packaging/aur
makepkg -f                                        # تجربة البناء محليًا
makepkg --printsrcinfo > .SRCINFO
```

ثم انسخ `PKGBUILD` و`.SRCINFO` إلى مستودع `ssh://aur@aur.archlinux.org/halawa-hub-bin.git`.

البرنامج المثبت من هذه الحزمة لا يحدّث نفسه: يعرض إشعار الإصدار الجديد ويفتح صفحته،
والتحديث يتم عبر مدير الحزم.
