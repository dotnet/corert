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

"%__DotNetCliPath%\dotnet.exe" msbuild "%__ProjectDir%\pkg\packages.proj" /m /nologo /flp:v=diag;LogFile=build-packages.log /p:NuPkgRid=%__NugetRuntimeId% /p:OSGroup=%__BuildOS% /p:Configuration=%__BuildType% /p:Platform=%__BuildArch% %__ExtraMsBuildParams%
exit /b %ERRORLEVEL%
