@if not defined _echo @echo off
setlocal

set _VSINSTALLER="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vs_installer.exe"
if not exist %_VSINSTALLER% (
echo Visual Studio installer not found. && echo Expected at %_VSINSTALLER% && exit /b 1
)
set _VSWHERE="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist %_VSWHERE% (
 for /f "usebackq tokens=*" %%i in (`%_VSWHERE% -latest -prerelease -property installationPath -products *`) do set _VSPATH="%%i"
)
if not exist %_VSPATH% (
echo Visual Studio installation not found. && exit /b 2
)

%_VSINSTALLER% modify %* --installPath %_VSPATH% --config "%~dp0..\.vsconfig"
