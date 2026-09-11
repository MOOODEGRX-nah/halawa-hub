@echo off
REM ============================================================
REM  يبني HalawaHub كبرنامج مستقل (exe واحد) تقدر تشغّله مباشرة
REM  بدون أي محرر أكواد أو IDE — فقط دبل-كليك على هذا الملف.
REM  يحتاج مرة واحدة فقط: .NET 8 SDK مثبت على جهازك.
REM  (تحميله من: https://dotnet.microsoft.com/download/dotnet/8.0)
REM ============================================================

echo جاري بناء HalawaHub...
echo.

dotnet publish src\HalawaHub.App\HalawaHub.App.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o publish

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo حدث خطأ أثناء البناء. راجع الرسائل بالأعلى.
    pause
    exit /b 1
)

echo جاري بناء الإضافات (Plugins)...
if not exist "publish\Plugins" mkdir "publish\Plugins"

dotnet build src\HalawaHub.Plugins.Sample\HalawaHub.Plugins.Sample.csproj -c Release -o build-temp-plugin >nul
copy /Y "build-temp-plugin\HalawaHub.Plugins.Sample.dll" "publish\Plugins\" >nul
rmdir /s /q build-temp-plugin

echo.
echo تم! البرنامج جاهز في مجلد publish\Halawa-Hub.exe
echo تقدر تسوي له اختصار على سطح المكتب وتشغّله مباشرة من الآن.
echo.

start "" explorer.exe "%~dp0publish"
pause
