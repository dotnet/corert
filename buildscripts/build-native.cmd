@if not defined _echo @echo off
setlocal EnableDelayedExpansion

set __ThisScriptShort=%0

if /i "%1" == "/?"    goto HelpVarCall
if /i "%1" == "-?"    goto HelpVarCall
if /i "%1" == "/h"    goto HelpVarCall
if /i "%1" == "-h"    goto HelpVarCall
if /i "%1" == "/help" goto HelpVarCall
if /i "%1" == "-help" goto HelpVarCall

if defined BUILDVARS_DONE goto :AfterVarSetup

goto :NormalVarCall

:HelpVarCall
call %~dp0buildvars-setup.cmd -help
exit /b 1

:NormalVarCall
call %~dp0buildvars-setup.cmd %*

IF NOT ERRORLEVEL 1 goto AfterVarSetup
echo Setting build variables failed.
exit /b %ERRORLEVEL%

:AfterVarSetup

echo Commencing build of native components for %__BuildOS%.%__BuildArch%.%__BuildType%
echo.

:PrepareVs
:: Set the environment for the native build
set __VCBuildArch=x86_amd64
if /i "%__BuildArch%" == "x86" (set __VCBuildArch=x86)

call "%VSINSTALLDIR%\VC\Auxiliary\Build\vcvarsall.bat" %__VCBuildArch%

:: Regenerate the build files
:RegenerateBuildFiles
pushd "%__IntermediatesDir%"
call "%__SourceDir%\Native\gen-buildsys-win.bat" "%__ProjectDir%\src\Native" %__VSVersion% %__BuildArch% %__BuildType%
popd

if exist "%__IntermediatesDir%\install.vcxproj" goto BuildNativeVs
if exist "%__IntermediatesDir%\Makefile" goto BuildNativeEmscripten
echo Failed to generate native component build project!
exit /b 1

:BuildNativeVs
%_msbuildexe% /ConsoleLoggerParameters:ForceNoAlign "%__IntermediatesDir%\install.vcxproj" %__ExtraMsBuildParams% /nologo /maxcpucount /nodeReuse:false /p:Configuration=%__BuildType% /p:Platform=%__BuildArch% /fileloggerparameters:Verbosity=normal;LogFile="%__NativeBuildLog%"
IF NOT ERRORLEVEL 1 goto AfterNativeBuild
echo Native component build failed. Refer !__NativeBuildLog! for details.
exit /b 1

:BuildNativeEmscripten
pushd "%__IntermediatesDir%"
nmake install
popd
IF NOT ERRORLEVEL 1 goto AfterNativeBuild
exit /b 1

:AfterNativeBuild
endlocal
