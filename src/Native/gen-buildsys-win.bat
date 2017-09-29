@echo off
rem
rem This file invokes cmake and generates the build system for windows.

set argC=0
for %%x in (%*) do Set /A argC+=1

if NOT %argC%==4 GOTO :USAGE
if %1=="/?" GOTO :USAGE

setlocal
set basePath=%~dp0
:: remove quotes
set "basePath=%basePath:"=%"
:: remove trailing slash
if %basePath:~-1%==\ set "basePath=%basePath:~0,-1%"

set __VSString=12 2013
if /i "%2" == "vs2015" (set __VSString=14 2015)
if /i "%2" == "vs2017" (set __VSString=15 2017)
if /i "%3" == "x64" (set __VSString=%__VSString% Win64)

if defined CMakePath goto DoGen

:: Eval the output from probe-win1.ps1
for /f "delims=" %%a in ('powershell -NoProfile -ExecutionPolicy ByPass "& %~dp0\probe-win.ps1"') do %%a

:DoGen
if "%3" == "wasm" (
  emcmake "%CMakePath%" "-DEMSCRIPTEN_GENERATE_BITCODE_STATIC_LIBRARIES=1" "-DCMAKE_TOOLCHAIN_FILE=%EMSCRIPTEN%/cmake/Modules/Platform/Emscripten.cmake" "-DCMAKE_BUILD_TYPE=%4" -G "NMake Makefiles" %1
) else (
  "%CMakePath%" "-DCLR_CMAKE_TARGET_ARCH=%3" -G "Visual Studio %__VSString%" %1
)
endlocal
GOTO :DONE

:USAGE
  echo "Usage..."
  echo "gen-buildsys-win.bat <path to top level CMakeLists.txt> <VSVersion>"
  echo "Specify the path to the top level CMake file - <corert>/src/Native"
  echo "Specify the VSVersion to be used - VS2013 or VS2015"
  echo "Specify the build type (Debug, Release)"
  EXIT /B 1

:DONE
  EXIT /B 0






