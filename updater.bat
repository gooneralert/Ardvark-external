@echo off
setlocal

set "OFFSETS_DIR=%~dp0src\Ardvark\offsets"
set "OFFSETS_URL=https://imtheo.lol/Offsets/Offsets.cs"
set "FFLAGS_URL=https://imtheo.lol/Offsets/FFlags.cs"
set "OFFSETS_FILE=%OFFSETS_DIR%\offsets.cs"
set "FFLAGS_FILE=%OFFSETS_DIR%\FFlags.cs"

echo [*] Updating offsets...

curl.exe -s -L -o "%OFFSETS_FILE%" "%OFFSETS_URL%"
if errorlevel 1 (
    echo [!] Failed to download offsets.cs
    pause
    exit /b 1
)
echo [+] offsets.cs updated

curl.exe -s -L -o "%FFLAGS_FILE%" "%FFLAGS_URL%"
if errorlevel 1 (
    echo [!] Failed to download FFlags.cs
    pause
    exit /b 1
)
echo [+] FFlags.cs updated

echo.
echo [*] Building...
dotnet publish "%~dp0src\Ardvark\Ardvark.csproj" -c Release -v minimal -r win-x64 -p:PublishSingleFile=true --self-contained true -o "%~dp0app"
if errorlevel 1 (
    echo [!] Build failed
    pause
    exit /b 1
)

echo.
echo [*] Cleaning build artifacts...
rmdir /S /Q "%~dp0src\Ardvark\bin" 2>nul
rmdir /S /Q "%~dp0src\Ardvark\obj" 2>nul
echo [+] Done. Ardvark.exe is in the app\ folder.

echo.
echo [*] Creating shortcut...
powershell -Command "$s=(New-Object -COM WScript.Shell).CreateShortcut('%~dp0Ardvark.lnk');$s.TargetPath='%~dp0app\Ardvark.exe';$s.WorkingDirectory='%~dp0app';$s.Save()"
echo [+] Shortcut created: Ardvark.lnk

echo.
echo [*] Done.
pause
