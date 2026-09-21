@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo Building Halawa-Hub...

dotnet publish src/HalawaHub.App/HalawaHub.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed for main app
    exit /b %ERRORLEVEL%
)

echo Building sample plugin...
dotnet build src/HalawaHub.Plugins.Sample/HalawaHub.Plugins.Sample.csproj -c Release -o build-plugin
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed for plugin
    exit /b %ERRORLEVEL%
)

if not exist publish\Plugins mkdir publish\Plugins
copy /Y build-plugin\HalawaHub.Plugins.Sample.dll publish\Plugins\ >nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to copy plugin
    exit /b %ERRORLEVEL%
)

echo ✅ Build successful! Check the publish\ folder.
pause
