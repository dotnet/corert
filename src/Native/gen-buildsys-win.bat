@echo off
rem
rem This file invokes cmake and generates the build system for windows.

set argC=0
for %%x in (%*) do Set /A argC+=1

if NOT %argC%==4 GOTO :USAGE
if %1=="/?" GOTO :USAGE

setlocal

set __CmakeGenerator=Visual Studio 15 2017
if /i "%2" == "vs2019" (set __CmakeGenerator=Visual Studio 16 2019)
if /i "%2" == "vs2017" (set __CmakeGenerator=Visual Studio 15 2017)
if /i "%3" == "x64" (set __ExtraCmakeParams=%__ExtraCmakeParams% -A x64)
if /i "%3" == "x86" (set __ExtraCmakeParams=%__ExtraCmakeParams% -A Win32)
if /i "%3" == "arm64" (set __ExtraCmakeParams=%__ExtraCmakeParams% -A ARM64)
if /i "%3" == "arm" (set __ExtraCmakeParams=%__ExtraCmakeParams% -A ARM)

if defined CMakePath goto DoGen

:: Eval the output from probe-win1.ps1
for /f "delims=" %%a in ('powershell -NoProfile -ExecutionPolicy ByPass "& %~dp0\probe-win.ps1"') do %%a

:DoGen
if "%3" == "wasm" (
  emcmake "%CMakePath%" "-DCMAKE_TOOLCHAIN_FILE=%EMSDK%/upstream/emscripten/cmake/Modules/Platform/Emscripten.cmake" "-DCLR_CMAKE_TARGET_ARCH=%3" "-DCMAKE_BUILD_TYPE=%4" -G "NMake Makefiles" %1
) else (
  "%CMakePath%" "-DCLR_CMAKE_TARGET_ARCH=%3" "-DOBJWRITER_BUILD=%__ObjWriterBuild%" %__ExtraCmakeParams% -G "%__CmakeGenerator%" %1
)
endlocal
GOTO :DONE

:USAGE
  echo "Usage..."
  echo "gen-buildsys-win.bat <path to top level CMakeLists.txt> <VSVersion>"
  echo "Specify the path to the top level CMake file - <corert>/src/Native"
  echo "Specify the VSVersion to be used - VS2017 or VS2019"
  echo "Specify the build type (Debug, Release)"
  EXIT /B 1

:DONE
  EXIT /B 0
