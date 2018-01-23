:: Test execution wrapper for CoreCLR tests
::
:: This wrapper is called from each CoreCLR test's .cmd / .sh run script as the custom test launcher.
:: We use this opportunity to invoke the CoreRT compiler and then run the produced native binary.
::
:: %1 contains test folder
:: %2 contains test exe name
::
@echo OFF
setlocal ENABLEDELAYEDEXPANSION

set TestFolder=%1

::
:: We're only interested in referencing the xunit runner - the test dlls will be imported by the test project
::
set TestExecutable=xunit.console.netcore
set TestFileName=%2


:: Copy artefacts necessary to compile and run the xunit exe
copy /Y "%~dp0\runtest\CoreFXTestHarness\*" "%TestFolder%"

:: Create log dir if it doesn't exist
if not exist %XunitLogDir% md %XunitLogDir%

if not exist %TestFolder%\%TestExecutable%.exe ( 
    :: Not a test we support yet, exit silently
    exit /b 0
)
:: Workaround until we have a better reflection engine
:: Add name of currently executing test to rd.xml
powershell -Command "(Get-Content %TestFolder%\default.rd.xml).replace('*Application*', '%TestFileName%') | Set-Content  %TestFolder%\default.rd.xml"

if "%CoreRT_BuildArch%" == "x64" (
    call "%VS140COMNTOOLS%\..\..\VC\bin\amd64\vcvars64.bat"
)
echo msbuild /ConsoleLoggerParameters:ForceNoAlign "/p:IlcPath=%CoreRT_ToolchainDir%" "/p:DebugSymbols=true" "/p:Configuration=%CoreRT_BuildType%" "/p:RepoLocalBuild=true" "/p:FrameworkLibPath=%~dp0..\..\bin\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\lib" "/p:FrameworkObjPath=%~dp0..\..\bin\obj\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\Framework" /p:DisableFrameworkLibGeneration=true /p:PackagesDir=%~dp0..\..\packages\ /p:ExecutableName=%TestExecutable% %TestFolder%\Test.csproj
call msbuild /ConsoleLoggerParameters:ForceNoAlign "/p:IlcPath=%CoreRT_ToolchainDir%" "/p:DebugSymbols=false" "/p:Configuration=%CoreRT_BuildType%" "/p:RepoLocalBuild=true" "/p:FrameworkLibPath=%~dp0..\..\bin\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\lib" "/p:FrameworkObjPath=%~dp0..\..\bin\obj\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\Framework" /p:DisableFrameworkLibGeneration=true /p:PackagesDir=%~dp0..\..\packages\ /p:ExecutableName=%TestExecutable% %TestFolder%\Test.csproj

echo %TestFolder%\native\%TestExecutable% %TestFolder%\%TestFileName%.dll
call %TestFolder%\native\%TestExecutable% %TestFolder%\%TestFileName%.dll -xml %XunitLogDir%\%TestFileName%.xml