@if not defined __echo @echo off
setlocal EnableDelayedExpansion

:: Set the default arguments
set __BuildArch=x64
set __BuildType=Debug
set __BuildOS=Windows_NT

:: Default to highest Visual Studio version available
if defined VS160COMNTOOLS (
    set __VSVersion=vs2019
    set __VSProductVersion=160
) else (
    set __VSVersion=vs2017
    set __VSProductVersion=150
)

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

set CORE_ROOT=%1
echo %__MsgPrefix%CORE_ROOT is initially set to: "%CORE_ROOT%"
shift 
:ArgsDone

set "__TestWorkingDir=%CoreRT_TestRoot\CoreFX%"

if not defined XunitTestBinBase set  XunitTestBinBase=%__TestWorkingDir%

if not exist %__LogsDir% md %__LogsDir%

:: Check presence of VS
if not defined VS%__VSProductVersion%COMNTOOLS goto NoVS

set __VSToolsRoot=!VS%__VSProductVersion%COMNTOOLS!
if %__VSToolsRoot:~-1%==\ set "__VSToolsRoot=%__VSToolsRoot:~0,-1%"

if not defined VSINSTALLDIR (
    echo %__MsgPrefix%Error: runtest.cmd should be run from a Visual Studio Command Prompt.  Please see https://github.com/dotnet/coreclr/blob/master/Documentation/project-docs/developer-guide.md for build instructions.
    exit /b 1
)

if not defined FXCustomTestLauncher (
    echo The CoreFX test launcher is not defined 
    exit /b 1
)

set SAVED_ERROR_LEVEL=0
:: Iterate through unzipped CoreFX tests 
for /D %%i in ("%XunitTestBinBase%\*" ) do (
    set TestFolderName=%%i
    set TestFileName=%%~nxi

    echo %FXCustomTestLauncher% !TestFolderName! !TestFileName!
    call %FXCustomTestLauncher% !TestFolderName! !TestFileName!
    set TestExitCode=!errorlevel!
    if !TestExitCode! neq 0 (
        echo Test !TestFileName! failed with !TestExitCode!
        set SAVED_ERROR_LEVEL=!TestExitCode!
    )

)

exit /b !SAVED_ERROR_LEVEL!

:Usage
echo.
echo Usage:
echo   %0 BuildArch BuildType
echo where:
echo.
echo./? -? /h -h /help -help: view this message.
echo BuildArch- Optional parameter - x64 or x86 ^(default: x64^).
echo BuildType- Optional parameter - Debug, Release, or Checked ^(default: Debug^).
exit /b 1

:NoVS
echo Visual Studio 2017 or 2019 ^(Community is free^) is a prerequisite to build this repository.
echo See: https://github.com/dotnet/coreclr/blob/master/Documentation/project-docs/developer-guide.md#prerequisites
exit /b 1
