@if "%_echo%" neq "on" echo off
setlocal EnableDelayedExpansion

set __ThisScriptShort=%0
set __ThisScriptFull="%~f0"

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

if /i "%1" == "clean"   (set __CleanBuild=1&shift&goto Arg_Loop)

if /i "%1" == "skiptests" (set __SkipTests=1&shift&goto Arg_Loop)
if /i "%1" == "skipvsdev" (set __SkipVsDev=1&shift&goto Arg_Loop)
if /i "%1" == "/milestone" (set __ToolchainMilestone=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/dotnetclipath" (set __DotNetCliPath=%2&shift&shift&goto Arg_Loop)

echo Invalid command line argument: %1
goto Usage

:ArgsDone

echo Commencing CoreRT Repo build
echo.

:: Set the remaining variables based upon the determined build configuration
set "__BinDir=%__RootBinDir%\Product\%__BuildOS%.%__BuildArch%.%__BuildType%"
set "__ObjDir=%__RootBinDir%\obj\%__BuildOS%.%__BuildArch%.%__BuildType%"
set "__IntermediatesDir=%__RootBinDir%\obj\Native\%__BuildOS%.%__BuildArch%.%__BuildType%\"
set "__RelativeProductBinDir=bin\Product\%__BuildOS%.%__BuildArch%.%__BuildType%"

set "__ReproProjectDir=%__ProjectDir%\src\ILCompiler\repro"
set "__ReproProjectBinDir=%__ReproProjectDir%\bin"
set "__ReproProjectObjDir=%__ReproProjectDir%\obj"

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

if exist "%__ReproProjectBinDir%" rd /s /q "%__ReproProjectBinDir%"
if exist "%__ReproProjectObjDir%" rd /s /q "%__ReproProjectObjDir%"

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
if exist "%__IntermediatesDir%\install.vcxproj" goto BuildNative
echo Failed to generate native component build project!
exit /b 1

:BuildNative
set "__NativeBuildLog=%__LogsDir%\Native_%__BuildOS%__%__BuildArch%__%__BuildType%.log"
%_msbuildexe% "%__IntermediatesDir%\install.vcxproj" %__MSBCleanBuildArgs% /nologo /maxcpucount /nodeReuse:false /p:Configuration=%__BuildType% /p:Platform=%__BuildArch% /fileloggerparameters:Verbosity=normal;LogFile="%__NativeBuildLog%"
IF NOT ERRORLEVEL 1 goto ManagedBuild
echo Native component build failed. Refer !__NativeBuildLog! for details.
exit /b 1

:ManagedBuild
REM endlocal to rid us of environment changes from vcvarsall.bat
endlocal

REM setlocal to prepare for vsdevcmd.bat
setlocal

rem Explicitly set Platform causes conflicts in managed project files. Clear it to allow building from VS x64 Native Tools Command Prompt
set Platform=

:: Restore the Tools directory
call  "%~dp0init-tools.cmd"

rem Tell nuget to always use repo-local nuget package cache. The "dotnet restore" invocations use the --packages
rem argument, but there are a few commands in publish and tests that do not.
set "NUGET_PACKAGES=%__PackagesDir%"

echo Using CLI tools version:
dir /b "%__DotNetCliPath%\sdk"

:: Set the environment for the managed build
:SetupManagedBuild
call "!VS%__VSProductVersion%COMNTOOLS!\VsDevCmd.bat"
echo Commencing build of managed components for %__BuildOS%.%__BuildArch%.%__BuildType%
echo.
set "__BuildLog=%__LogsDir%\msbuild_%__BuildOS%__%__BuildArch%__%__BuildType%.log"
%_msbuildexe% "%__ProjectDir%\build.proj" %__MSBCleanBuildArgs% /p:RepoPath="%__ProjectDir%" /p:RepoLocalBuild="true" /p:RelativeProductBinDir="%__RelativeProductBinDir%" /p:ToolchainMilestone=%__ToolchainMilestone% /nologo /maxcpucount /verbosity:minimal /nodeReuse:false /fileloggerparameters:Verbosity=normal;LogFile="%__BuildLog%"
IF NOT ERRORLEVEL 1 (
  findstr /ir /c:".*Warning(s)" /c:".*Error(s)" /c:"Time Elapsed.*" "%__BuildLog%"
  goto AfterILCompilerBuild
)
echo ILCompiler build failed. Refer !__BuildLog! for details.
exit /b 1


:AfterILCompilerBuild
setlocal
rem Workaround for --appdepsdkpath command line switch being ignored.
rem Copy the restored appdepsdk package to its default location.
pushd "%__ProjectDir%\tests"
call testenv.cmd %__BuildType% %__BuildArch%
popd
set /p DOTNET_VERSION=< "%~dp0DotnetCLIVersion.txt"
xcopy /S /Y "%CoreRT_AppDepSdkDir%" "%__DotNetCliPath%\sdk\%DOTNET_VERSION%\appdepsdk%\"
endlocal

:VsDevGenerateRespFiles
if defined __SkipVsDev goto :AfterVsDevGenerateRespFiles
set __GenRespFiles=0
if not exist "%__ObjDir%\ryujit.rsp" set __GenRespFiles=1
if not exist "%__ObjDir%\cpp.rsp" set __GenRespFiles=1
if "%__GenRespFiles%"=="1" (
    setlocal
    pushd "%__ProjectDir%\tests"
    call testenv.cmd %__BuildType% %__BuildArch%
    popd
    call "!VS140COMNTOOLS!\..\..\VC\vcvarsall.bat" %__BuildArch%

    "%__DotNetCliPath%\dotnet.exe" build --native --ilcpath "%__BinDir%\packaging\publish1" --appdepsdkpath "%CoreRT_AppDepSdkDir%" "%__ReproProjectDir%" -c %__BuildType%
    call :CopyResponseFile "%__ReproProjectObjDir%\%__BuildType%\dnxcore50\native\dotnet-compile-native-ilc.rsp" "%__ObjDir%\ryujit.rsp"

    rem Workaround for https://github.com/dotnet/cli/issues/1956
    rmdir /s /q "%__ReproProjectBinDir%"
    rmdir /s /q "%__ReproProjectObjDir%"

    set __AdditionalCompilerFlags=
    if /i "%__BuildType%"=="debug" (
        set __AdditionalCompilerFlags=--cppcompilerflags /MTd
    )
    "%__DotNetCliPath%\dotnet.exe" build --native --cpp --ilcpath "%__BinDir%\packaging\publish1" --appdepsdkpath "%CoreRT_AppDepSdkDir%" "%__ReproProjectDir%" -c %__BuildType% !__AdditionalCompilerFlags!
    call :CopyResponseFile "%__ReproProjectObjDir%\%__BuildType%\dnxcore50\native\dotnet-compile-native-ilc.rsp" "%__ObjDir%\cpp.rsp"
    endlocal
)
:AfterVsDevGenerateRespFiles

:RunTests
if defined __SkipTests exit /b 0

pushd "%__ProjectDir%\tests"
call "runtest.cmd" %__BuildType% %__BuildArch% /dotnetclipath %__DotNetCliPath%
set TEST_EXIT_CODE=%ERRORLEVEL%
popd
exit /b %TEST_EXIT_CODE%

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

rem Copies the dotnet generated response file while patching up references
rem to System.Private assemblies to the live built ones.
rem This is to make sure that making changes in a private library doesn't require
rem a full rebuild. It also helps with locating the symbols.
:CopyResponseFile
setlocal
> %~2 (
  for /f "tokens=*" %%l in (%~1) do (
    set line=%%l
    if "!line:publish1\sdk=!"=="!line!" (
        echo !line!
    ) ELSE (
        set assemblyPath=!line:~3!
        call :ExtractFileName !assemblyPath! assemblyFileName
        echo -r:%__BinDir%\!assemblyFileName!\!assemblyFileName!.dll
    )
  )
)
endlocal
goto:eof

rem Extracts a file name from a full path
rem %1 Full path to the file, %2 Variable to receive the file name
:ExtractFileName
setlocal
for %%i in ("%1") DO set fileName=%%~ni
endlocal & set "%2=%fileName%"
goto:eof
