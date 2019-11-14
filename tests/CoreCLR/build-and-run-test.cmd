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

set TestFolder=%1

::
:: The CoreRT build targets expect a test file name with no extension
::
set TestExecutable=%2
set TestFileName=%~n2

copy /Y %~dp0\Test.csproj %TestFolder%

:: Cleanup previous test run artifacts since the test may have been run with /NoCleanup
rd /s /q %TestFolder%\native

::
:: The CoreCLR test system configures the VS environment as 32-bit by default,
:: so override if we're doing a 64-bit test run
::
if "%CoreRT_BuildArch%" == "x64" (
    call "%VSINSTALLDIR%\VC\Auxiliary\Build\vcvarsall.bat" x64
)

set MsBuildCommandLine=msbuild /ConsoleLoggerParameters:ForceNoAlign "/p:IlcPath=%CoreRT_ToolchainDir%" "/p:Configuration=%CoreRT_BuildType%" "/p:RepoLocalBuild=true" "/p:FrameworkLibPath=%~dp0..\..\bin\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\lib" "/p:FrameworkObjPath=%~dp0..\..\bin\obj\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\Framework" /p:DisableFrameworkLibGeneration=true "/p:CoreRT_CoreCLRRuntimeDir=%CoreRT_CoreCLRRuntimeDir%" %TestFolder%\Test.csproj
echo %MsBuildCommandLine%
%MsBuildCommandLine%
if errorlevel 1 (
    set TestExitCode=!ERRORLEVEL!
    goto :Cleanup
)

:: Some tests (interop) have native artifacts they depend on. Copy all DLLs to be sure we have them.
copy %TestFolder%\*.dll %TestFolder%\native\

:: Remove the first two parameters passed by the test cmd file which are added to communicate test
:: information to custom test runners
shift
shift

set TestParameters=
:GetNextParameter
if "%1"=="" goto :RunTest
set "TestParameters=%TestParameters%%Delimiter%%1"
set "Delimiter= "
shift
goto :GetNextParameter

:RunTest

%TestFolder%\native\%TestExecutable% %TestParameters%

set TestExitCode=!ERRORLEVEL!

:Cleanup

::
:: We must clean up the native artifacts (binary, obj, pdb) as we go. Across the ~7000 
:: CoreCLR pri-0 tests at ~50MB of native artifacts per test, we can easily use 300GB
:: of disk space and clog up the CI machines
::
if not "%CoreRT_NoCleanup%"=="true" (
    rd /s /q %TestFolder%\native
)


::
:: Dev bring-up aid to help populate the inclusion / exclusion files. Ie, tests\ReadyToRun.CoreCLR.issues.targets.
:: All failing tests will have a file named exclude.txt containing the test cmd file path.
:: All passing tests will have a file named include.txt containing the test cmd file path.
::
:: From <root>\tests_downloaded\CoreCLR gather all the test cmd file names:
::
::  for /r %f in (include.txt) do type "%f" >> passed.txt
::
::   Or
::
::  for /r %f in (exclude.txt) do type "%f" >> failed.txt
::
:: You can find / replace to massage the list into the XML format needed in the issues.targets file.
::
del %TestFolder%\include.txt
del %TestFolder%\exclude.txt

if "!TestExitCode!" == "100" (
    echo %TestFolder%%TestFileName%.cmd > %TestFolder%\include.txt
) else (
    echo %TestFolder%%TestFileName%.cmd > %TestFolder%\exclude.txt
)

exit /b !TestExitCode!
