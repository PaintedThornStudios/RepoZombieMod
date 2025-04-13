@echo off
setlocal enabledelayedexpansion

:: Set version and mod name from manifest.json
for /f "tokens=2 delims=:" %%a in ('type manifest.json ^| findstr "version_number"') do (
    set "version=%%a"
    set "version=!version:~2,-2!"
)
for /f "tokens=2 delims=:" %%a in ('type manifest.json ^| findstr "name"') do (
    set "modname=%%a"
    set "modname=!modname:~2,-2!"
)

:: Set author
set "author=CarsonJF"

:: Create output directory if it doesn't exist
if not exist "D:\Games\Unity\RepoMods\BuiltMods" mkdir "D:\Games\Unity\RepoMods\BuiltMods"

:: Create temporary directory for packaging
set "tempdir=%TEMP%\mod_package_%RANDOM%"
mkdir "%tempdir%"

:: Copy files to temp directory
copy /Y "bin\Debug\netstandard2.1\%modname%.dll" "%tempdir%\"
copy /Y "manifest.json" "%tempdir%\"
copy /Y "README.md" "%tempdir%\"
if exist "icon.png" copy /Y "icon.png" "%tempdir%\"
if exist "CHANGELOG.md" copy /Y "CHANGELOG.md" "%tempdir%\"

:: Copy each asset bundle individually
for %%f in ("AssetBundles\*.bundle") do (
    copy /Y "%%f" "%tempdir%\%modname%.bundle"
)

:: Create zip file
powershell Compress-Archive -Path "%tempdir%\*" -DestinationPath "D:\Games\Unity\RepoMods\BuiltMods\%author%-%modname%-%version%.zip" -Force

:: Clean up
rmdir /S /Q "%tempdir%"

echo Package created successfully: %author%-%modname%-%version%.zip