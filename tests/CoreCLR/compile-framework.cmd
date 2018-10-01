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

for %%x in (%CoreRT_CoreCLRRuntimeDir%\Microsoft.*.dll %CoreRT_CoreCLRRuntimeDir%\System.*.dll) do (
    echo %%x
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
set MsBuildCommandLine=msbuild /ConsoleLoggerParameters:ForceNoAlign "/p:IlcPath=%CoreRT_ToolchainDir%" "/p:Configuration=%CoreRT_BuildType%" "/p:RepoLocalBuild=true" "/p:FrameworkLibPath=%~dp0..\..\bin\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\lib" "/p:FrameworkObjPath=%~dp0..\..\bin\obj\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\Framework" /p:DisableFrameworkLibGeneration=true /t:CopyNative %~dp0CompileAssembly.csproj
echo %MsBuildCommandLine%
%MsBuildCommandLine%

set /a SavedErrorLevel=%ErrorLevel%

if "%SavedErrorLevel%" == "1000" (
    echo %1 is not a managed assembly.
    exit /b 0
)

if %SavedErrorLevel% neq 0 (
    exit /b !ERRORLEVEL!
)

goto :eof
