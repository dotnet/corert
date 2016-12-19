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

rem Tell nuget to always use repo-local nuget package cache. The "dotnet restore" invocations use the --packages
rem argument, but there are a few commands in publish and tests that do not.
set "NUGET_PACKAGES=%__PackagesDir%"

echo Using CLI tools version:
dir /b "%__DotNetCliPath%\sdk"

:: Set the environment for the managed build
:SetupManagedBuild
call "!VS%__VSProductVersion%COMNTOOLS!\VsDevCmd.bat"
echo Commencing build of managed components for %__BuildOS%.%__BuildArch%.%__BuildType%
echo.
%_msbuildexe% /ConsoleLoggerParameters:ForceNoAlign "%__ProjectDir%\build.proj" %__MSBCleanBuildArgs% %__ExtraMsBuildParams% /p:RepoPath="%__ProjectDir%" /p:RepoLocalBuild="true" /p:RelativeProductBinDir="%__RelativeProductBinDir%" /p:NuPkgRid=win7-x64 /p:ToolchainMilestone=%__ToolchainMilestone% /nologo /maxcpucount /verbosity:minimal /nodeReuse:false /fileloggerparameters:Verbosity=normal;LogFile="%__BuildLog%"
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
    if exist "%__ReproProjectBinDir%" rd /s /q "%__ReproProjectBinDir%"
    if exist "%__ReproProjectObjDir%" rd /s /q "%__ReproProjectObjDir%"

    %_msbuildexe% /ConsoleLoggerParameters:ForceNoAlign "/p:IlcPath=%__BinDir%\packaging\publish1" /p:Configuration=%__BuildType% /t:IlcCompile "%__ReproProjectDir%\repro.csproj"
    call :CopyResponseFile "%__ReproProjectObjDir%\native\ilc.rsp" "%__ObjDir%\ryujit.rsp"

    if exist "%__ReproProjectBinDir%" rd /s /q "%__ReproProjectBinDir%"
    if exist "%__ReproProjectObjDir%" rd /s /q "%__ReproProjectObjDir%"

    set __ExtraArgs=/p:NativeCodeGen=cpp
    if /i "%__BuildType%"=="debug" (
        set __ExtraArgs=!__ExtraArgs! "/p:AdditionalCppCompilerFlags=/MTd"
    )
    %_msbuildexe% /ConsoleLoggerParameters:ForceNoAlign "/p:IlcPath=%__BinDir%\packaging\publish1" /p:Configuration=%__BuildType% /t:IlcCompile "%__ReproProjectDir%\repro.csproj" !__ExtraArgs!
    call :CopyResponseFile "%__ReproProjectObjDir%\native\ilc.rsp" "%__ObjDir%\cpp.rsp"
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