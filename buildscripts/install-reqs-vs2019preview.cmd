@if not defined _echo @echo off
setlocal

set _VSINSTALLER="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vs_installer.exe"
if not exist %_VSINSTALLER% (
echo Visual Studio installer not found. && echo Expected at %_VSINSTALLER% && exit /b 1
)
set _VSPATH="%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Preview"
if not exist %_VSPATH% (
echo Visual Studio 2019 Preview not found. && echo Expected at %_VSPATH% && exit /b 2
)

%_VSINSTALLER% modify %* --installPath %_VSPATH% --config "%~dp0..\.vsconfig"
