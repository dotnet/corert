::
:: Produce ready-to-run images for the framework assemblies
::
:: Report methods that were not compilable
::

@if not defined _echo @echo off
setlocal EnableDelayedExpansion

rd /s /q %CoreRT_CoreCLRRuntimeDir%\native

if "%CoreRT_CoreCLRRuntimeDir%" == "" (
    echo set CoreRT_CoreCLRRuntimeDir to CoreCLR folder or run from runtest.cmd
    exit /b 1
)

for %%x in (%CoreRT_CoreCLRRuntimeDir%\Microsoft.*.dll %CoreRT_CoreCLRRuntimeDir%\System.*.dll) do (
    call :CompileAssembly %%x
)

goto :eof

::
:: Compile a single framework assembly
::
:: Parameters:
:: %1 Path to assembly to compile
:CompileAssembly

echo Compiling %1
set TestFileName=%1
set MsBuildCommandLine=msbuild 
set MsBuildCommandLine=%MsBuildCommandLine% /ConsoleLoggerParameters:ForceNoAlign
set MsBuildCommandLine=%MsBuildCommandLine% "/p:IlcPath=%CoreRT_ToolchainDir%"
set MsBuildCommandLine=%MsBuildCommandLine% "/p:Configuration=%CoreRT_BuildType%"
set MsBuildCommandLine=%MsBuildCommandLine% "/p:RepoLocalBuild=true"
set MsBuildCommandLine=%MsBuildCommandLine% /t:CopyNative
set MsBuildCommandLine=%MsBuildCommandLine% %~dp0CompileAssembly.csproj

echo %MsBuildCommandLine%
%MsBuildCommandLine%

set /a SavedErrorLevel=%ErrorLevel%

if %SavedErrorLevel% neq 0 (
    exit /b !ERRORLEVEL!
)

goto :eof
