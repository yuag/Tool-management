@echo off
setlocal
cd /d "%~dp0"

echo === win-x64 ===
dotnet publish src\QuickLaunch.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\win-x64
if errorlevel 1 exit /b 1

echo === linux-x64 ===
dotnet publish src\QuickLaunch.App -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\linux-x64
if errorlevel 1 exit /b 1

echo === osx-x64 (Intel Mac) ===
dotnet publish src\QuickLaunch.App -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\osx-x64
if errorlevel 1 exit /b 1

echo === osx-arm64 (Apple Silicon) ===
dotnet publish src\QuickLaunch.App -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\osx-arm64
if errorlevel 1 exit /b 1

echo.
echo Done.
dir publish
endlocal
