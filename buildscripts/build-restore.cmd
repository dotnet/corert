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

:: Restore the Tools directory
call  "%__ProjectDir%\init-tools.cmd"

"%__DotNetCliPath%\dotnet.exe" msbuild "%__ProjectDir%\build.proj" /nologo /t:Restore /flp:v=normal;LogFile=build-restore.log /p:NuPkgRid=win7-x64 /maxcpucount /p:OSGroup=%__BuildOS% /p:Configuration=%__BuildType% /p:Platform=%__BuildArch% %__ExtraMsBuildParams%
IF ERRORLEVEL 1 goto ErrorExit

rem Buildtools tooling is not capable of publishing netcoreapp currently. Use helper projects to publish skeleton of
rem the standalone app that the build injects actual binaries into later.
"%__DotNetCliPath%\dotnet.exe" restore "%__SourceDir%\ILCompiler\netcoreapp\ilc.csproj" -r win7-x64
IF ERRORLEVEL 1 goto ErrorExit
"%__DotNetCliPath%\dotnet.exe" publish "%__SourceDir%\ILCompiler\netcoreapp\ilc.csproj" -r win7-x64 -o "%__RootBinDir%\%__BuildOS%.%__BuildArch%.%__BuildType%\tools"
IF ERRORLEVEL 1 goto ErrorExit

exit /b 0

:ErrorExit
exit /b 1
