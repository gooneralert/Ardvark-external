@echo off
setlocal

echo [*] Checking for git...
where git >nul 2>&1
if errorlevel 1 (
    echo [!] Git is not installed or not in PATH. Please install Git from https://git-scm.com/
    pause
    exit /b 1
)

echo [*] Pulling latest changes from repository...
git -C "%~dp0." pull https://github.com/gooneralert/Ardvark-external
if errorlevel 1 (
    echo [!] Git pull failed. Ensure you have network access and the repository is configured correctly.
    pause
    exit /b 1
)
echo [+] Repository up to date.

echo.
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
echo [*] Saving version info...
copy /Y "%~dp0version.txt" "%~dp0app\version.txt" >nul
echo [+] version.txt updated.

echo.
echo [*] Done.
pause
