:: Set the default arguments for build
set __BuildArch=x64
set __BuildType=Debug
set __BuildOS=Windows_NT
set __HostOS=Windows_NT

:: Disable telemetry, first time experience, and global sdk look for the CLI
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_MULTILEVEL_LOOKUP=0

:: Set the various build properties here so that CMake and MSBuild can pick them up
set "__ProjectDir=%~dp0.."
:: remove trailing slash
if %__ProjectDir:~-1%==\ set "__ProjectDir=%__ProjectDir:~0,-1%"
set "__SourceDir=%__ProjectDir%\src"
set "__RootBinDir=%__ProjectDir%\bin"
set "__LogsDir=%__RootBinDir%\Logs"
set __SkipTestBuild=
set "__DotNetCliPath=%__ProjectDir%\Tools\dotnetcli"

set __ObjWriterBuild=0

:Arg_Loop
if "%1" == "" goto ArgsDone

if /i "%1" == "/?"    goto Usage
if /i "%1" == "-?"    goto Usage
if /i "%1" == "/h"    goto Usage
if /i "%1" == "-h"    goto Usage
if /i "%1" == "/help" goto Usage
if /i "%1" == "-help" goto Usage

if /i "%1" == "x64"    (set __BuildArch=x64&&shift&goto Arg_Loop)
if /i "%1" == "x86"    (set __BuildArch=x86&&shift&goto Arg_Loop)
if /i "%1" == "arm"    (set __BuildArch=arm&&shift&goto Arg_Loop)
if /i "%1" == "wasm"    (set __BuildOS=WebAssembly&&set __BuildArch=wasm&&shift&goto Arg_Loop)

if /i "%1" == "debug"    (set __BuildType=Debug&shift&goto Arg_Loop)
if /i "%1" == "release"   (set __BuildType=Release&shift&goto Arg_Loop)

if /i "%1" == "clean"   (set __CleanBuild=1&shift&goto Arg_Loop)

if /i "%1" == "skiptests" (set __SkipTests=1&shift&goto Arg_Loop)
if /i "%1" == "buildtests" (set __BuildTests=1&shift&goto Arg_Loop)
if /i "%1" == "skipvsdev" (set __SkipVsDev=1&shift&goto Arg_Loop)
if /i "%1" == "objwriter" (set __ObjWriterBuild=1&set "__ExtraMsBuildParams=%__ExtraMsBuildParams% /p:ObjWriterBuild=true"&shift&goto Arg_Loop)
if /i "%1" == "/dotnetclipath" (set __DotNetCliPath=%2&shift&shift&goto Arg_Loop)

if /i "%1" == "/officialbuildid" (set "__ExtraMsBuildParams=%__ExtraMsBuildParams% /p:OfficialBuildId=%2"&shift&shift&goto Arg_Loop)

echo Invalid command line argument: %1
exit /b 1
:ArgsDone

:: Set the remaining variables based upon the determined build configuration
set "__BinDir=%__RootBinDir%\%__BuildOS%.%__BuildArch%.%__BuildType%"
set "__ObjDir=%__RootBinDir%\obj\%__BuildOS%.%__BuildArch%.%__BuildType%"
set "__IntermediatesDir=%__RootBinDir%\obj\Native\%__BuildOS%.%__BuildArch%.%__BuildType%\"
set "__NativeBuildLog=%__LogsDir%\Native_%__BuildOS%__%__BuildArch%__%__BuildType%.log"
set "__BuildLog=%__LogsDir%\msbuild_%__BuildOS%__%__BuildArch%__%__BuildType%.log"
set "__TestBuildLog=%__LogsDir%\tests_%__BuildOS%__%__BuildArch%__%__BuildType%.log"

:: Generate path to be set for CMAKE_INSTALL_PREFIX to contain forward slash
set "__CMakeBinDir=%__BinDir%"
set "__CMakeBinDir=%__CMakeBinDir:\=/%"

:: Configure environment if we are doing a clean build.
if not defined __CleanBuild goto MakeDirs
echo Doing a clean build
echo.

:: Cleanup the previous output for the selected configuration
if exist "%__BinDir%" rd /s /q "%__BinDir%"
if exist "%__ObjDir%" rd /s /q "%__ObjDir%"
if exist "%__IntermediatesDir%" rd /s /q "%__IntermediatesDir%"

if exist "%__LogsDir%" del /f /q "%__LogsDir%\*_%__BuildOS%__%__BuildArch%__%__BuildType%.*"

:MakeDirs
if not exist "%__BinDir%" md "%__BinDir%"
if not exist "%__ObjDir%" md "%__ObjDir%"
if not exist "%__IntermediatesDir%" md "%__IntermediatesDir%"
if not exist "%__LogsDir%" md "%__LogsDir%"

:: Check prerequisites
echo Checking pre-requisites...
echo.

if "%__BuildArch%"=="wasm" (
    goto :CheckPrereqsEmscripten
) else (
    goto :CheckPrereqsVs
)

:CheckPrereqsEmscripten
if not defined EMSDK (
    echo Emscripten is a prerequisite to build for WebAssembly.
    echo See: https://github.com/dotnet/corert/blob/master/Documentation/how-to-build-WebAssembly.md
    exit /b 1
)
goto CheckPrereqsVs

:CheckPrereqsVs
:: Validate that PowerShell is accessibile.
for %%X in (powershell.exe) do (set __PSDir=%%~$PATH:X)
if defined __PSDir goto EvaluatePS
echo PowerShell is a prerequisite to build this repository.
echo See: https://github.com/dotnet/corert/blob/master/Documentation/prerequisites-for-building.md
exit /b 1

:EvaluatePS
:: Eval the output from probe-win1.ps1
for /f "delims=" %%a in ('powershell -NoProfile -ExecutionPolicy ByPass "& ""%__SourceDir%\Native\probe-win.ps1"""') do %%a

:: Default to highest Visual Studio version available
::
:: For VS2017 and later, multiple instances can be installed on the same box SxS and VS1*0COMNTOOLS
:: is no longer set as a global environment variable and is instead only set if the user
:: has launched the Visual Studio Developer Command Prompt.
::
:: Following this logic, we will default to the Visual Studio toolset assocated with the active
:: Developer Command Prompt. Otherwise, we will query VSWhere to locate the later version of
:: Visual Studio available on the machine. Finally, we will fail the script if not supported
:: instance can be found.

if defined VisualStudioVersion goto :RunVCVars

set _VSWHERE="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist %_VSWHERE% (
  for /f "usebackq tokens=*" %%i in (`%_VSWHERE% -latest -prerelease -property installationPath -products *`) do set _VSCOMNTOOLS=%%i\Common7\Tools
)
if not exist "%_VSCOMNTOOLS%" goto :MissingVersion

call "%_VSCOMNTOOLS%\VsDevCmd.bat"

:RunVCVars
if "%VisualStudioVersion%"=="16.0" (
    goto :VS2019
) else if "%VisualStudioVersion%"=="15.0" (
    goto :VS2017
)

:MissingVersion
:: Can't find VS 2017 or 2019
echo Visual Studio 2017 or 2019 is a pre-requisite to build this repository.
echo See: https://github.com/dotnet/corert/blob/master/Documentation/prerequisites-for-building.md
exit /b 1

:VS2017
:: Setup vars for VS2017
set __VSVersion=vs2017
set __VSProductVersion=150
if not exist "%VSINSTALLDIR%\VC\Auxiliary\Build\vcvarsall.bat" goto :MissingVisualC
set _msbuildexe="%VSINSTALLDIR%\MSBuild\15.0\Bin\MSBuild.exe"
goto :CheckMSBuild

:VS2019
:: Setup vars for VS2019
set __VSVersion=vs2019
set __VSProductVersion=160
if not exist "%VSINSTALLDIR%\VC\Auxiliary\Build\vcvarsall.bat" goto :MissingVisualC
set _msbuildexe="%VSINSTALLDIR%\MSBuild\Current\Bin\MSBuild.exe"
goto :CheckMSBuild

:MissingVisualC
echo Could not find Visual C++ under !VS%__VSProductVersion%COMNTOOLS!. Visual C++ is a pre-requisite to build this repository.
echo See: https://github.com/dotnet/corert/blob/master/Documentation/prerequisites-for-building.md
exit /b 1

:CheckMSBuild
if not exist !_msbuildexe! (echo Error: Could not find MSBuild.exe.  Please see https://github.com/dotnet/corert/blob/master/Documentation/prerequisites-for-building.md for build instructions. && exit /b 1)

rem Explicitly set Platform causes conflicts in managed project files. Clear it to allow building from VS x64 Native Tools Command Prompt
set Platform=

:: Set the environment for the native build
set __VCBuildArch=x86_amd64
if /i "%__BuildArch%" == "x86" (set __VCBuildArch=x86)

set __NugetRuntimeId=win-x64
if /i "%__BuildArch%" == "x86" (set __NugetRuntimeId=win-x86)

:Done
set BUILDVARS_DONE=1
exit /b 0

:Usage
echo.
echo Build the CoreRT repo.
echo.
echo Usage:
echo     %__ThisScriptShort% [option1] [option2] ...
echo.
echo All arguments are optional. The options are:
echo.
echo./? -? /h -h /help -help: view this message.
echo Build architecture: one of x64, x86, arm, wasm ^(default: x64^).
echo Build type: one of Debug, Checked, Release ^(default: Debug^).
echo clean: force a clean build ^(default is to perform an incremental build^).
echo skiptests: skip building tests ^(default: tests are built^).
exit /b 1
