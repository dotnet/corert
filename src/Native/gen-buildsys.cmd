@echo off
rem
rem This file invokes cmake and generates the build system for windows.

set "__ProjectDir=%~dp0..\.."
set "__RootBinDir=%__ProjectDir%\bin"

set __BuildOS=Windows_NT
set __BuildArch=%1
set __BuildType=%2
set "__IntermediatesDir=%__RootBinDir%\obj\Native\%__BuildOS%.%__BuildArch%.%__BuildType%\"
echo Intermediates: %__IntermediatesDir%
echo 1: %1 2: %2 3: %3
if not exist %__IntermediatesDir% mkdir %__IntermediatesDir%
pushd %__IntermediatesDir%

setlocal
set basePath=%~dp0
:: remove quotes
set "basePath=%basePath:"=%"
:: remove trailing slash
if %basePath:~-1%==\ set "basePath=%basePath:~0,-1%"

set __VSString=14 2015
if /i "%1" == "x64" (set __VSString=%__VSString% Win64)

if defined CMakePath goto DoGen

:: Eval the output from probe-win1.ps1
for /f "delims=" %%a in ('powershell -NoProfile -ExecutionPolicy RemoteSigned "& %basePath%\probe-win.ps1"') do %%a

:DoGen
"%CMakePath%" "-DCLR_CMAKE_TARGET_ARCH=%1" -G "Visual Studio %__VSString%" %basePath%
endlocal
GOTO :DONE

:DONE
  popd
  EXIT /B 0






