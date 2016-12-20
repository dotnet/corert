:: Set the default arguments for build
set __BuildArch=x64
set __BuildType=Debug
set __BuildOS=Windows_NT

:: Default to highest Visual Studio version available
set __VSVersion=vs2015

:: Set the various build properties here so that CMake and MSBuild can pick them up
set "__ProjectDir=%~dp0.."
:: remove trailing slash
if %__ProjectDir:~-1%==\ set "__ProjectDir=%__ProjectDir:~0,-1%"
set "__SourceDir=%__ProjectDir%\src"
set "__PackagesDir=%__ProjectDir%\packages"
set "__RootBinDir=%__ProjectDir%\bin"
set "__LogsDir=%__RootBinDir%\Logs"
set __MSBCleanBuildArgs=
set __SkipTestBuild=
set __ToolchainMilestone=testing
set "__DotNetCliPath=%__ProjectDir%\Tools\dotnetcli"

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

if /i "%1" == "debug"    (set __BuildType=Debug&shift&goto Arg_Loop)
if /i "%1" == "release"   (set __BuildType=Release&shift&goto Arg_Loop)

if /i "%1" == "vs2017"   (set __VSVersion=vs2017&shift&goto Arg_Loop)

if /i "%1" == "clean"   (set __CleanBuild=1&shift&goto Arg_Loop)

if /i "%1" == "skiptests" (set __SkipTests=1&shift&goto Arg_Loop)
if /i "%1" == "skipvsdev" (set __SkipVsDev=1&shift&goto Arg_Loop)
if /i "%1" == "/milestone" (set __ToolchainMilestone=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/dotnetclipath" (set __DotNetCliPath=%2&shift&shift&goto Arg_Loop)

if /i "%1" == "/officialbuildid" (set "__ExtraMsBuildParams=/p:OfficialBuildId=%2"&shift&shift&goto Arg_Loop)

echo Invalid command line argument: %1
exit /b 1
:ArgsDone

:: Set the remaining variables based upon the determined build configuration
set "__BinDir=%__RootBinDir%\Product\%__BuildOS%.%__BuildArch%.%__BuildType%"
set "__ObjDir=%__RootBinDir%\obj\%__BuildOS%.%__BuildArch%.%__BuildType%"
set "__IntermediatesDir=%__RootBinDir%\obj\Native\%__BuildOS%.%__BuildArch%.%__BuildType%\"
set "__RelativeProductBinDir=bin\Product\%__BuildOS%.%__BuildArch%.%__BuildType%"
set "__NativeBuildLog=%__LogsDir%\Native_%__BuildOS%__%__BuildArch%__%__BuildType%.log"
set "__BuildLog=%__LogsDir%\msbuild_%__BuildOS%__%__BuildArch%__%__BuildType%.log"
set "__ReproProjectDir=%__ProjectDir%\src\ILCompiler\repro"
set "__ReproProjectBinDir=%__BinDir%\repro"
set "__ReproProjectObjDir=%__ObjDir%\repro"

:: Generate path to be set for CMAKE_INSTALL_PREFIX to contain forward slash
set "__CMakeBinDir=%__BinDir%"
set "__CMakeBinDir=%__CMakeBinDir:\=/%"

:: Configure environment if we are doing a clean build.
if not defined __CleanBuild goto MakeDirs
echo Doing a clean build
echo.

:: MSBuild projects would need a rebuild
set __MSBCleanBuildArgs=/t:rebuild /p:CleanedTheBuild=1

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

:CheckPrereqs
:: Check prerequisites
echo Checking pre-requisites...
echo.

:: Validate that PowerShell is accessibile.
for %%X in (powershell.exe) do (set __PSDir=%%~$PATH:X)
if defined __PSDir goto EvaluatePS
echo PowerShell is a prerequisite to build this repository.
echo See: https://github.com/dotnet/corert/blob/master/Documentation/prerequisites-for-building.md
exit /b 1

:EvaluatePS
:: Eval the output from probe-win1.ps1
for /f "delims=" %%a in ('powershell -NoProfile -ExecutionPolicy ByPass "& ""%__SourceDir%\Native\probe-win.ps1"""') do %%a


set __VSProductVersion=
if /i "%__VSVersion%" == "vs2015" set __VSProductVersion=140
if /i "%__VSVersion%" == "vs2017" set __VSProductVersion=150

:: Check presence of VS
if defined VS%__VSProductVersion%COMNTOOLS goto CheckVSExistence
echo Visual Studio 2015 (Community is free) is a pre-requisite to build this repository.
echo See: https://github.com/dotnet/corert/blob/master/Documentation/prerequisites-for-building.md
exit /b 1

:CheckVSExistence
:: Does VS VS 2015 really exist?
if exist "!VS%__VSProductVersion%COMNTOOLS!\..\IDE\devenv.exe" goto CheckMSBuild
echo Visual Studio 2015 (Community is free) is a pre-requisite to build this repository.
echo See: https://github.com/dotnet/corert/blob/master/Documentation/prerequisites-for-building.md
exit /b 1


:CheckMSBuild
:: Note: We've disabled node reuse because it causes file locking issues.
::       The issue is that we extend the build with our own targets which
::       means that that rebuilding cannot successfully delete the task
::       assembly. 
if /i "%__VSVersion%" == "vs2017" (
	:: The MSBuild that is installed in the shared location is not compatible
	:: with VS2017 C++ projects. I must use the MSBuild located in
	:: C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe
	:: which is compatible. However, I don't know a good way to specify this
	:: path in a way that isn't specific to my system, so I am relying on the
	:: system PATH to locate this tool.
	set _msbuildexe=msbuild
) else (
	set _msbuildexe="%ProgramFiles(x86)%\MSBuild\14.0\Bin\MSBuild.exe"
	if not exist !_msbuildexe! (set _msbuildexe="%ProgramFiles%\MSBuild\14.0\Bin\MSBuild.exe")
	if not exist !_msbuildexe! (echo Error: Could not find MSBuild.exe.  Please see https://github.com/dotnet/corert/blob/master/Documentation/prerequisites-for-building.md for build instructions. && exit /b 1)
)

rem Explicitly set Platform causes conflicts in managed project files. Clear it to allow building from VS x64 Native Tools Command Prompt
set Platform=

:: Set the environment for the native build
set __VCBuildArch=x86_amd64
if /i "%__BuildArch%" == "x86" (set __VCBuildArch=x86)

rem Tell nuget to always use repo-local nuget package cache. The "dotnet restore" invocations use the --packages
rem argument, but there are a few commands in publish and tests that do not.
set "NUGET_PACKAGES=%__PackagesDir%"

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
echo Build architecture: one of x64, x86, arm ^(default: x64^).
echo Build type: one of Debug, Checked, Release ^(default: Debug^).
echo Visual Studio version: ^(default: VS2015^).
echo clean: force a clean build ^(default is to perform an incremental build^).
echo skiptests: skip building tests ^(default: tests are built^).
exit /b 1