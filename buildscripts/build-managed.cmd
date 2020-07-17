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

rem Explicitly set Platform causes conflicts in managed project files. Clear it to allow building from VS x64 Native Tools Command Prompt
set Platform=

:: Restore the Tools directory
call  "%__ProjectDir%\init-tools.cmd"

echo Using CLI tools version:
dir /b "%__DotNetCliPath%\sdk"

"%__DotNetCliPath%\dotnet.exe" msbuild "%__ProjectDir%\build.proj" /nologo /t:Restore /flp:v=normal;LogFile="%__BuildLog%" /p:NuPkgRid=%__NugetRuntimeId% /maxcpucount /p:OSGroup=%__BuildOS% /p:Configuration=%__BuildType% /p:Platform=%__BuildArch% %__ExtraMsBuildParams%
IF ERRORLEVEL 1 exit /b %ERRORLEVEL%

rem Buildtools tooling is not capable of publishing netcoreapp currently. Use helper projects to publish skeleton of
rem the standalone app that the build injects actual binaries into later.
"%__DotNetCliPath%\dotnet.exe" restore "%__SourceDir%\ILCompiler\netcoreapp\ilc.csproj" -r %__NugetRuntimeId%
IF ERRORLEVEL 1 exit /b %ERRORLEVEL%
"%__DotNetCliPath%\dotnet.exe" publish "%__SourceDir%\ILCompiler\netcoreapp\ilc.csproj" -r %__NugetRuntimeId% -o "%__RootBinDir%\%__BuildOS%.%__BuildArch%.%__BuildType%\tools"
IF ERRORLEVEL 1 exit /b %ERRORLEVEL%

:: Set the environment for the managed build
call "!VS%__VSProductVersion%COMNTOOLS!\VsDevCmd.bat"
echo Commencing build of managed components for %__BuildOS%.%__BuildArch%.%__BuildType%
echo.
"%__DotNetCliPath%\dotnet.exe" msbuild /ConsoleLoggerParameters:ForceNoAlign "%__ProjectDir%\build.proj" %__ExtraMsBuildParams% /p:RepoPath="%__ProjectDir%" /p:RepoLocalBuild="true" /p:NuPkgRid=%__NugetRuntimeId% /nologo /maxcpucount /verbosity:minimal /nodeReuse:false /fileloggerparameters:Verbosity=normal;LogFile="%__BuildLog%"
IF NOT ERRORLEVEL 1 (
  findstr /ir /c:".*Warning(s)" /c:".*Error(s)" /c:"Time Elapsed.*" "%__BuildLog%"
  goto AfterILCompilerBuild
)
echo ILCompiler build failed with exit code %ERRORLEVEL%. Refer !__BuildLog! for details.
exit /b %ERRORLEVEL%

:AfterILCompilerBuild

:VsDevGenerateRespFiles
if defined __SkipVsDev goto :AfterVsDevGenerateRespFiles
set __GenRespFiles=0
if not exist "%__ObjDir%\ryujit.rsp" set __GenRespFiles=1
if not exist "%__ObjDir%\cpp.rsp" set __GenRespFiles=1
if "%__GenRespFiles%"=="1" (
    "%__DotNetCliPath%\dotnet.exe" msbuild /ConsoleLoggerParameters:ForceNoAlign "/p:IlcPath=%__BinDir%" /p:Configuration=%__BuildType% /t:Clean,IlcCompile "%__ProjectDir%\src\ILCompiler\repro\repro.csproj"
    call :CopyResponseFile "%__ObjDir%\repro\native\repro.ilc.rsp" "%__ObjDir%\ryujit.rsp"

    set __ExtraArgs=/p:NativeCodeGen=cpp
    if /i "%__BuildType%"=="debug" (
        set __ExtraArgs=!__ExtraArgs! "/p:AdditionalCppCompilerFlags=/MTd"
    )
    "%__DotNetCliPath%\dotnet.exe" msbuild /ConsoleLoggerParameters:ForceNoAlign "/p:IlcPath=%__BinDir%" /p:Configuration=%__BuildType% /t:Clean,IlcCompile "%__ProjectDir%\src\ILCompiler\repro\repro.csproj" !__ExtraArgs!
    call :CopyResponseFile "%__ObjDir%\repro\native\repro.ilc.rsp" "%__ObjDir%\cpp.rsp"
)
:AfterVsDevGenerateRespFiles
exit /b %ERRORLEVEL%
endlocal

rem Copies the dotnet generated response file while patching up references
rem to System.Private assemblies to the live built ones.
rem This is to make sure that making changes in a private library doesn't require
rem a full rebuild. It also helps with locating the symbols.
:CopyResponseFile
setlocal
> %~2 (
  for /f "tokens=*" %%l in (%~1) do (
    set line=%%l
    if "!line:publish1\sdk=!"=="!line!" (
        echo !line!
    ) ELSE (
        set assemblyPath=!line:~3!
        call :ExtractFileName !assemblyPath! assemblyFileName
        echo -r:%__BinDir%\!assemblyFileName!\!assemblyFileName!.dll
    )
  )
)
endlocal
goto:eof

rem Extracts a file name from a full path
rem %1 Full path to the file, %2 Variable to receive the file name
:ExtractFileName
setlocal
for %%i in ("%1") DO set fileName=%%~ni
endlocal & set "%2=%fileName%"
goto:eof
