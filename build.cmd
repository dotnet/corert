@echo off
setlocal EnableDelayedExpansion

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

:Arg_Loop
if "%1" == "" goto ArgsDone
if /i "%1" == "/?" goto Usage
if /i "%1" == "x64"    (set __BuildArch=x64&&shift&goto Arg_Loop)
if /i "%1" == "x86"    (set __BuildArch=x86&&shift&goto Arg_Loop)
if /i "%1" == "arm"    (set __BuildArch=arm&&shift&goto Arg_Loop)

if /i "%1" == "debug"    (set __BuildType=Debug&shift&goto Arg_Loop)
if /i "%1" == "release"   (set __BuildType=Release&shift&goto Arg_Loop)

if /i "%1" == "clean"   (set __CleanBuild=1&shift&goto Arg_Loop)

if /i "%1" == "skiptestbuild" (set __SkipTestBuild=1&shift&goto Arg_Loop)

echo Invalid command line argument: %1
goto Usage

:ArgsDone

echo Commencing CoreRT Repo build
echo.

:: Set the remaining variables based upon the determined build configuration
set "__BinDir=%__RootBinDir%\Product\%__BuildOS%.%__BuildArch%.%__BuildType%"
set "__IntermediatesDir=%__RootBinDir%\obj\Native\%__BuildOS%.%__BuildArch%.%__BuildType%\"

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
if exist "%__IntermediatesDir%" rd /s /q "%__IntermediatesDir%"

if exist "%__LogsDir%" del /f /q "%__LogsDir%\*_%__BuildOS%__%__BuildArch%__%__BuildType%.*"

:MakeDirs
if not exist "%__BinDir%" md "%__BinDir%"
if not exist "%__IntermediatesDir%" md "%__IntermediatesDir%"
if not exist "%__LogsDir%" md "%__LogsDir%"

:CheckPrereqs
:: Check prerequisites
echo Checking pre-requisites...
echo.
:: Eval the output from probe-win1.ps1
for /f "delims=" %%a in ('powershell -NoProfile -ExecutionPolicy RemoteSigned "& ""%__SourceDir%\Native\probe-win.ps1"""') do %%a

:CheckVS

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

:: All set to commence the build

setlocal
echo Commencing build of native components for %__BuildOS%.%__BuildArch%.%__BuildType%
echo.

:: Set the environment for the native build
set __VCBuildArch=x86_amd64
if /i "%__BuildArch%" == "x86" (set __VCBuildArch=x86)
call "!VS%__VSProductVersion%COMNTOOLS!\..\..\VC\vcvarsall.bat" %__VCBuildArch%

:: Regenerate the VS solution
pushd "%__IntermediatesDir%"
call "%__SourceDir%\Native\gen-buildsys-win.bat" "%__ProjectDir%\src\Native" %__VSVersion% %__BuildArch% 
popd

:BuildComponents
if exist "%__IntermediatesDir%\install.vcxproj" goto BuildCoreRT
echo Failed to generate native component build project!
exit /b 1

REM Build CoreRT
:BuildCoreRT
set "__CoreRTBuildLog=%__LogsDir%\CoreRT_%__BuildOS%__%__BuildArch%__%__BuildType%.log"
%_msbuildexe% "%__IntermediatesDir%\install.vcxproj" %__MSBCleanBuildArgs% /nologo /maxcpucount /nodeReuse:false /p:Configuration=%__BuildType% /p:Platform=%__BuildArch% /fileloggerparameters:Verbosity=normal;LogFile="%__CoreRTBuildLog%"
IF NOT ERRORLEVEL 1 goto ManagedBuild
echo Native component build failed. Refer !__CoreRTBuildLog! for details.
exit /b 1

:ManagedBuild
REM endlocal to rid us of environment changes from vcvarsall.bat
endlocal

REM setlocal to prepare for vsdevcmd.bat
setlocal

rem Explicitly set Platform causes conflicts in managed project files. Clear it to allow building from VS x64 Native Tools Command Prompt
set Platform=

:: Set the environment for the managed build
call "!VS%__VSProductVersion%COMNTOOLS!\VsDevCmd.bat"
echo Commencing build of managed components for %__BuildOS%.%__BuildArch%.%__BuildType%
echo.
set "__ILCompilerBuildLog=%__LogsDir%\ILCompiler_%__BuildOS%__%__BuildArch%__%__BuildType%.log"
%_msbuildexe% "%__ProjectDir%\build.proj" %__MSBCleanBuildArgs% /nologo /maxcpucount /verbosity:minimal /nodeReuse:false /fileloggerparameters:Verbosity=normal;LogFile="%__ILCompilerBuildLog%"
IF NOT ERRORLEVEL 1 (
  findstr /ir /c:".*Warning(s)" /c:".*Error(s)" /c:"Time Elapsed.*" "%__ILCompilerBuildLog%"
  goto AfterILCompilerBuild
)
echo ILCompiler build failed. Refer !__ILCompilerBuildLog! for details.
exit /b 1


:AfterILCompilerBuild
if defined __SkipTestBuild exit /b 0

pushd "%__ProjectDir%\tests"
call "runtest.cmd" %__BuildType% %__BuildArch%
set TEST_EXIT_CODE=%ERRORLEVEL%
popd
exit /b %TEST_EXIT_CODE%
