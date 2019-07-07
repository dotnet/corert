:: Test build and execution wrapper for CoreFX tests
::
:: This wrapper is called for each of CoreFX's tests by runtest.cmd
::
:: %1 contains test folder
:: %2 contains test exe name
::
@echo OFF
setlocal ENABLEDELAYEDEXPANSION

set TestFolder=%1

::
:: We're only interested in referencing the xunit runner - the test dlls will be imported by the test wrapper project
::
set TestExecutable=xunit.console.netcore
set TestFileName=%2


:: Copy the artefacts we need to compile and run the xunit exe
copy /Y "%~dp0\runtest\CoreFXTestHarness\*" "%TestFolder%" >nul

if "%CoreRT_TestLogFileName%"=="" (
    set CoreRT_TestLogFileName=testResults.xml
)

:: Create log dir if it doesn't exist
if not exist %XunitLogDir% md %XunitLogDir%

if not exist %XunitLogDir%\%TestFileName% md %XunitLogDir%\%TestFileName%

if not exist %TestFolder%\%TestExecutable%.exe ( 
    :: Not a test we support, exit silently
    exit /b 0
)

:: Workaround until we have a better reflection engine
:: Add name of currently executing test to rd.xml
powershell -Command "(Get-Content %TestFolder%\default.rd.xml).replace('*Application*', '%TestFileName%') | Set-Content  %TestFolder%\default.rd.xml"

::
:: Force environment to 64-bit if we're doing an x64 test run
::
if "%CoreRT_BuildArch%" == "x64" (
    call "%VSINSTALLDIR%\VC\Auxiliary\Build\vcvarsall.bat" x64
)

echo Building %TestFileName%

call "%CoreRT_CliDir%\dotnet.exe" publish %TestFolder%\Test.csproj /ConsoleLoggerParameters:ForceNoAlign "/p:IlcPath=%CoreRT_ToolchainDir%" "/p:DebugSymbols=false" "/p:Configuration=%CoreRT_BuildType%" "/p:FrameworkLibPath=%~dp0..\..\bin\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\lib" "/p:FrameworkObjPath=%~dp0..\..\bin\obj\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\Framework" /p:DisableFrameworkLibGeneration=true /p:TestRootDir=%~dp0  /p:OSGroup=%CoreRT_BuildOS% /p:ExecutableName=%TestExecutable% /nologo
if errorlevel 1 (
    echo Building %TestFileName% failed
    exit /b 1
)

set ExtraXUnitArgs=
if "%CoreRT_SingleThreaded%"=="true" (
    set ExtraXUnitArgs=-parallel none
)

echo Executing %TestFileName% - writing logs to %XunitLogDir%\%TestFileName%\%CoreRT_TestLogFileName%
echo To repro directly, run call %TestFolder%\native\%TestExecutable% %TestFolder%\%TestFileName%.dll @"%TestFolder%\%TestFileName%.rsp" -xml %XunitLogDir%\%TestFileName%\%CoreRT_TestLogFileName% -notrait category=nonnetcoreapptests -notrait category=nonwindowstests  -notrait category=failing %ExtraXUnitArgs%

if not exist "%TestFolder%\native\%TestExecutable%".exe (
    echo ERROR:Native binary not found Unable to run test.
    exit /b 1
)

call %TestFolder%\native\%TestExecutable% %TestFolder%\%TestFileName%.dll @"%TestFolder%\%TestFileName%.rsp" -xml %XunitLogDir%\%TestFileName%\%CoreRT_TestLogFileName% -notrait category=nonnetcoreapptests -notrait category=nonwindowstests  -notrait category=failing %ExtraXUnitArgs%
set TestExitCode=!ERRORLEVEL!

exit /b %TestExitCode%
