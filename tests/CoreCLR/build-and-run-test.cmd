::
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

echo CoreRT_ToolchainDir %CoreRT_ToolchainDir%

set TestFolder=%1

::
:: The CoreRT build targets expect a test file name with no extension
::
set TestExecutable=%2
set TestFileName=%~n2

copy /Y %~dp0\Test.csproj %TestFolder%

::
:: Clear Core_Root before invoking MSBuild. This is important, because we need it set to the CoreCLR
:: test payload's CoreCLR folder while running the test infrastructure, but when msbuild launches
:: the CoreRT CoreCLR to run ILC.exe, Core_Root affects the binder and we probe for assemblies in
:: the wrong place.
set CORE_ROOT=

::
:: The CoreCLR test system configures the VS environment as 32-bit by default,
:: so override if we're doing a 64-bit test run
::
if "%CoreRT_BuildArch%" == "x64" (
    call "%VS140COMNTOOLS%\..\..\VC\bin\amd64\vcvars64.bat"
)

echo msbuild /ConsoleLoggerParameters:ForceNoAlign "/p:IlcPath=%CoreRT_ToolchainDir%" "/p:Configuration=%CoreRT_BuildType%" %TestFolder%\Test.csproj
msbuild /ConsoleLoggerParameters:ForceNoAlign "/p:IlcPath=%CoreRT_ToolchainDir%" "/p:Configuration=%CoreRT_BuildType%" %TestFolder%\Test.csproj
if errorlevel 1 (
    exit /b !ERRORLEVEL!
)

:: Remove the first two parameters passed by the test cmd file which are added to communicate test
:: information to custom test runners
shift
shift

set TestParameters=
set Delimiter=
:GetNextParameter
if "%1"=="" goto :RunTest
set "TestParameters=%TestParameters%%Delimiter%%1"
set "Delimiter= "
shift
goto :GetNextParameter

:RunTest
%TestFolder%\native\%TestExecutable% %TestParameters%

set TestExitCode=!ERRORLEVEL!

::
:: We must clean up the native artifacts (binary, obj, pdb) as we go. Across the ~7000 
:: CoreCLR pri-0 tests at ~50MB of native artifacts per test, we can easily use 300GB
:: of disk space and clog up the CI machines
::
rd /s /q %TestFolder%\native

exit /b !TestExitCode!
