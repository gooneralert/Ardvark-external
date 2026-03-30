@echo off
setlocal

set "REPO_ZIP_URL=https://github.com/gooneralert/Ardvark-external/archive/refs/heads/main.zip"
set "TEMP_ZIP=%TEMP%\ardvark_update.zip"
set "TEMP_EXTRACT=%TEMP%\ardvark_update_extract"
set "SELF=%~dp0."

echo [*] Downloading latest update...
powershell -Command "Invoke-WebRequest -Uri '%REPO_ZIP_URL%' -OutFile '%TEMP_ZIP%'" 2>nul
if errorlevel 1 (
    echo [!] Failed to download update. Check your internet connection.
    pause
    exit /b 1
)
echo [+] Download complete.

echo [*] Extracting update...
if exist "%TEMP_EXTRACT%" rmdir /S /Q "%TEMP_EXTRACT%"
powershell -Command "Expand-Archive -Path '%TEMP_ZIP%' -DestinationPath '%TEMP_EXTRACT%' -Force" 2>nul
if errorlevel 1 (
    echo [!] Failed to extract update.
    pause
    exit /b 1
)

echo [*] Applying update...
powershell -Command "Copy-Item -Path '%TEMP_EXTRACT%\Ardvark-external-main\*' -Destination '%SELF%' -Recurse -Force"
if errorlevel 1 (
    echo [!] Failed to apply update.
    pause
    exit /b 1
)
echo [+] Repository up to date.

rmdir /S /Q "%TEMP_EXTRACT%" 2>nul
del /Q "%TEMP_ZIP%" 2>nul

echo.
set "OFFSETS_DIR=%~dp0src\Ardvark\offsets"
set "OFFSETS_URL=https://imtheo.lol/Offsets/Offsets.cs"
set "FFLAGS_URL=https://imtheo.lol/Offsets/FFlags.cs"
set "OFFSETS_FILE=%OFFSETS_DIR%\offsets.cs"
set "FFLAGS_FILE=%OFFSETS_DIR%\FFlags.cs"

if not exist "%OFFSETS_DIR%" mkdir "%OFFSETS_DIR%"

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
