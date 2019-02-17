::
:: Produce ready-to-run images for the framework assemblies
::
:: Report methods that were not compilable
::

@if not defined _echo @echo off
setlocal EnableDelayedExpansion

if "%CoreRT_CoreCLRRuntimeDir%" == "" (
    echo set CoreRT_CoreCLRRuntimeDir to CoreCLR folder or run from runtest.cmd
    exit /b 1
)

rd /s /q %CoreRT_CoreCLRRuntimeDir%\native

mkdir %CoreRT_CoreCLRRuntimeDir%\native
xcopy /Q /Y %CoreRT_CoreCLRRuntimeDir%\*.exe %CoreRT_CoreCLRRuntimeDir%\native\
xcopy /Q /Y %CoreRT_CoreCLRRuntimeDir%\*.dll %CoreRT_CoreCLRRuntimeDir%\native\

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

:: Explicit exclusions for wrong files picked up by the wildcard spec
if /I [%~n1] == [Microsoft.DiaSymReader.Native.amd64] goto :eof
if /I [%~n1] == [System.Runtime.WindowsRuntime] goto :eof
if /I [%~n1] == [System.Runtime.WindowsRuntime.UI.Xaml] goto :eof

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
