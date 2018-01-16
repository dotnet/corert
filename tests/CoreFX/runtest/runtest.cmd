@if not defined __echo @echo off
setlocal EnableDelayedExpansion

:: Set the default arguments
set __BuildArch=x64
set __BuildType=Debug
set __BuildOS=Windows_NT

:: Default to highest Visual Studio version available
set __VSVersion=vs2017
set __VSProductVersion=150

:: Define a prefix for most output progress messages that come from this script. That makes
:: it easier to see where these are coming from. Note that there is a trailing space here.
set __MsgPrefix=RUNTEST: 

set __ProjectDir=%~dp0
:: remove trailing slash
if %__ProjectDir:~-1%==\ set "__ProjectDir=%__ProjectDir:~0,-1%"
set "__ProjectFilesDir=%__ProjectDir%"
set "__RootBinDir=%__ProjectDir%\..\bin"
set "__LogsDir=%__RootBinDir%\Logs"

:Arg_Loop
if "%1" == "" goto ArgsDone

if /i "%1" == "/?"    goto Usage
if /i "%1" == "-?"    goto Usage
if /i "%1" == "/h"    goto Usage
if /i "%1" == "-h"    goto Usage
if /i "%1" == "/help" goto Usage
if /i "%1" == "-help" goto Usage

if /i "%1" == "x64"                   (set __BuildArch=x64&set __MSBuildBuildArch=x64&shift&goto Arg_Loop)
if /i "%1" == "x86"                   (set __BuildArch=x86&set __MSBuildBuildArch=x86&shift&goto Arg_Loop)

if /i "%1" == "debug"                 (set __BuildType=Debug&shift&goto Arg_Loop)
if /i "%1" == "release"               (set __BuildType=Release&shift&goto Arg_Loop)

if /i "%1" == "LogsDir"         (set __LogsDir=%2&shift&shift&goto Arg_Loop) 

if /i not "%1" == "msbuildargs" goto SkipMsbuildArgs
:: All the rest of the args will be collected and passed directly to msbuild.
:CollectMsbuildArgs
shift
if "%1"=="" goto ArgsDone
set __msbuildExtraArgs=%__msbuildExtraArgs% %1
goto CollectMsbuildArgs
:SkipMsbuildArgs

set CORE_ROOT=%1
echo %__MsgPrefix%CORE_ROOT is initially set to: "%CORE_ROOT%"
shift 
:ArgsDone

set "__TestWorkingDir=%CoreRT_TestRoot\CoreFX%"

if not defined XunitTestBinBase       set  XunitTestBinBase=%__TestWorkingDir%

if not exist %__LogsDir% md %__LogsDir%

:: Check presence of VS
if not defined VS%__VSProductVersion%COMNTOOLS goto NoVS

set __VSToolsRoot=!VS%__VSProductVersion%COMNTOOLS!
if %__VSToolsRoot:~-1%==\ set "__VSToolsRoot=%__VSToolsRoot:~0,-1%"

set _msbuildexe="%VSINSTALLDIR%\MSBuild\15.0\Bin\MSBuild.exe"

if not exist !_msbuildexe! (echo Error: Could not find MSBuild.exe.  Please see https://github.com/dotnet/corert/blob/master/Documentation/prerequisites-for-building.md for build instructions. && exit /b 1)

:: Set the environment for the  build- VS cmd prompt
echo %__MsgPrefix%Using environment: "%__VSToolsRoot%\VsDevCmd.bat"
call                                 "%__VSToolsRoot%\VsDevCmd.bat"

if not defined VSINSTALLDIR (
    echo %__MsgPrefix%Error: runtest.cmd should be run from a Visual Studio Command Prompt.  Please see https://github.com/dotnet/coreclr/blob/master/Documentation/project-docs/developer-guide.md for build instructions.
    exit /b 1
)

:: Note: We've disabled node reuse because it causes file locking issues.
::       The issue is that we extend the build with our own targets which
::       means that that rebuilding cannot successfully delete the task
::       assembly. 
set __msbuildCommonArgs=/nologo /nodeReuse:false %__msbuildExtraArgs% /p:Platform=%__MSBuildBuildArch%

if not defined __Sequential (
    set __msbuildCommonArgs=%__msbuildCommonArgs% /maxcpucount
) else (
    set __msbuildCommonArgs=%__msbuildCommonArgs% /p:ParallelRun=false
)

:: Iterate through unzipped CoreFX tests 
for /D %%i in ("%XunitTestBinBase%\*" ) do (
    set TestFolderName=%%i
    set TestFileName=%%~nxi
    echo %FXCustomTestLauncher% !TestFolderName! !TestFileName!
    call %FXCustomTestLauncher% !TestFolderName! !TestFileName!
)