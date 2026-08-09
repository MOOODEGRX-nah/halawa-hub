# Halawa-Hub

**الإصدار: v0.0.8 (Beta)**

البرنامج يفحص نفسه عن تحديثات تلقائيًا عند التشغيل ويعرض لك رابط تحميل مباشر لو فيه إصدار أحدث.

لانشر ألعاب شخصي يجمع مكتبتك من عدة منصات في مكان واحد.

## المنصات المدعومة
- Steam
- GOG
- Epic Games
- Riot Games (League of Legends, VALORANT, Legends of Runeterra)
- Xbox / Microsoft Store (كشف تقريبي)

## تحميل البرنامج
1. تبويب **Actions** بالمستودع
2. آخر بناء ناجح (علامة صح خضراء) → قسم **Artifacts** → **Halawa-Hub**
3. فك الضغط وشغّل `Halawa-Hub.exe` مباشرة (ما يحتاج أي تثبيت)

## أغلفة لمنصات غير Steam (اختياري)
Steam يجيب غلافه تلقائيًا بدون إعداد. لباقي المنصات (GOG, Epic, Riot, Xbox):

1. سجّل حساب مجاني بـ [SteamGridDB](https://www.steamgriddb.com/profile/preferences) وخذ مفتاح API من تبويب API
2. شغّل البرنامج مرة وحدة (ينشئ ملف إعدادات تلقائيًا بـ
   `%LocalAppData%\HalawaHub\config.json`)
3. افتح الملف وحط مفتاحك بحقل `SteamGridDbApiKey`
4. أعد تشغيل البرنامج
