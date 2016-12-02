:: Set the default arguments for build
set __BuildArch=x64
set __BuildType=Debug
set __BuildOS=Windows_NT

:: Default to highest Visual Studio version available
set __VSVersion=vs2015

:: Set the various build properties here so that CMake and MSBuild can pick them up
set "__ProjectDir=%~dp0"
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

if /i "%1" == "x64"    (set __BuildArch=x64&&shift&goto Arg_Loop)
if /i "%1" == "x86"    (set __BuildArch=x86&&shift&goto Arg_Loop)
if /i "%1" == "arm"    (set __BuildArch=arm&&shift&goto Arg_Loop)

if /i "%1" == "debug"    (set __BuildType=Debug&shift&goto Arg_Loop)
if /i "%1" == "release"   (set __BuildType=Release&shift&goto Arg_Loop)

if /i "%1" == "clean"   (set __CleanBuild=1&shift&goto Arg_Loop)

if /i "%1" == "skiptests" (set __SkipTests=1&shift&goto Arg_Loop)
if /i "%1" == "skipvsdev" (set __SkipVsDev=1&shift&goto Arg_Loop)
if /i "%1" == "/milestone" (set __ToolchainMilestone=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/dotnetclipath" (set __DotNetCliPath=%2&shift&shift&goto Arg_Loop)

echo Invalid command line argument: %1

:ArgsDone

:: Set the remaining variables based upon the determined build configuration
set "__BinDir=%__RootBinDir%\Product\%__BuildOS%.%__BuildArch%.%__BuildType%"
set "__ObjDir=%__RootBinDir%\obj\%__BuildOS%.%__BuildArch%.%__BuildType%"
set "__IntermediatesDir=%__RootBinDir%\obj\Native\%__BuildOS%.%__BuildArch%.%__BuildType%\"
set "__RelativeProductBinDir=bin\Product\%__BuildOS%.%__BuildArch%.%__BuildType%"

set "__ReproProjectDir=%__ProjectDir%\src\ILCompiler\repro"
set "__ReproProjectBinDir=%__BinDir%\repro"
set "__ReproProjectObjDir=%__ObjDir%\repro"

:: Generate path to be set for CMAKE_INSTALL_PREFIX to contain forward slash
set "__CMakeBinDir=%__BinDir%"
set "__CMakeBinDir=%__CMakeBinDir:\=/%"

if not exist "%__BinDir%" md "%__BinDir%"
if not exist "%__ObjDir%" md "%__ObjDir%"
if not exist "%__IntermediatesDir%" md "%__IntermediatesDir%"
if exist "%__LogsDir%" del /f /q "%__LogsDir%\*_%__BuildOS%__%__BuildArch%__%__BuildType%.*"
if not exist "%__LogsDir%" md "%__LogsDir%"

:: Validate that PowerShell is accessibile.
for %%X in (powershell.exe) do (set __PSDir=%%~$PATH:X)
if defined __PSDir goto EvaluatePS
echo PowerShell is a prerequisite to build this repository.
echo See: https://github.com/dotnet/corert/blob/master/Documentation/prerequisites-for-building.md
exit /b 1

:EvaluatePS
:: Eval the output from probe-win1.ps1
for /f "delims=" %%a in ('powershell -NoProfile -ExecutionPolicy RemoteSigned "& ""%__SourceDir%\Native\probe-win.ps1"""') do %%a


set __VSProductVersion=
if /i "%__VSVersion%" == "vs2015" set __VSProductVersion=140

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
set _msbuildexe="%ProgramFiles(x86)%\MSBuild\14.0\Bin\MSBuild.exe"
if not exist %_msbuildexe% set _msbuildexe="%ProgramFiles%\MSBuild\14.0\Bin\MSBuild.exe"
if not exist %_msbuildexe% echo Error: Could not find MSBuild.exe.  Please see https://github.com/dotnet/corert/blob/master/Documentation/prerequisites-for-building.md for build instructions. && exit /b 1

rem Explicitly set Platform causes conflicts in managed project files. Clear it to allow building from VS x64 Native Tools Command Prompt
set Platform=

:: Set the environment for the native build
set __VCBuildArch=x86_amd64
if /i "%__BuildArch%" == "x86" (set __VCBuildArch=x86)

:: Cleanup the previous output for the selected configuration
if exist "%__BinDir%" rd /s /q "%__BinDir%"
if exist "%__ObjDir%" rd /s /q "%__ObjDir%"
if exist "%__IntermediatesDir%" rd /s /q "%__IntermediatesDir%"

rem Tell nuget to always use repo-local nuget package cache. The "dotnet restore" invocations use the --packages
rem argument, but there are a few commands in publish and tests that do not.
set "NUGET_PACKAGES=%__PackagesDir%"