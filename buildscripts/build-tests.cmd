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

if defined __SkipTests exit /b 0

echo "%__DotNetCliPath%\dotnet.exe" msbuild "%__ProjectDir%\tests\external\dirs.proj" /nologo /t:Restore /flp:v=normal;LogFile="%__TestBuildLog%" /p:NuPkgRid=%__NugetRuntimeId% /maxcpucount /p:OSGroup=%__BuildOS% /p:Configuration=%__BuildType% /p:Platform=%__BuildArch% %__ExtraMsBuildParams%
"%__DotNetCliPath%\dotnet.exe" msbuild "%__ProjectDir%\tests\external\dirs.proj" /nologo /t:Restore /flp:v=normal;LogFile="%__TestBuildLog%" /p:NuPkgRid=%__NugetRuntimeId% /maxcpucount /p:OSGroup=%__BuildOS% /p:Configuration=%__BuildType% /p:Platform=%__BuildArch% %__ExtraMsBuildParams%
IF ERRORLEVEL 1 exit /b %ERRORLEVEL%

call "!VS%__VSProductVersion%COMNTOOLS!\VsDevCmd.bat"
echo Commencing build of test components for %__BuildOS%.%__BuildArch%.%__BuildType%
echo.
"%__DotNetCliPath%\dotnet.exe" msbuild /ConsoleLoggerParameters:ForceNoAlign "%__ProjectDir%\tests\external\dirs.proj" %__ExtraMsBuildParams% /p:Configuration=%__BuildType% /p:Platform=%__BuildArch% /p:RepoPath="%__ProjectDir%" /p:RepoLocalBuild="true" /p:NuPkgRid=%__NugetRuntimeId% /nologo /maxcpucount /verbosity:minimal /nodeReuse:false /fileloggerparameters:Verbosity=normal;LogFile="%__TestBuildLog%"
IF NOT ERRORLEVEL 1 (
  findstr /ir /c:".*Warning(s)" /c:".*Error(s)" /c:"Time Elapsed.*" "%__TestBuildLog%"
  goto AfterTestBuild
)
echo Test build failed with exit code %ERRORLEVEL%. Refer !__TestBuildLog! for details.
exit /b %ERRORLEVEL%
:AfterTestBuild

if defined __BuildTests exit /b 0

pushd "%__ProjectDir%\tests"
call "runtest.cmd" %__BuildType% %__BuildArch% /dotnetclipath %__DotNetCliPath%
set TEST_EXIT_CODE=%ERRORLEVEL%
popd
exit /b %TEST_EXIT_CODE%
