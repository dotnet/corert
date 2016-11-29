@if not defined _echo @echo off
setlocal EnableDelayedExpansion

set __ThisScriptShort=%0
set __ThisScriptFull="%~f0"

call %~dp0buildvars-setup.cmd %*
if "%__CleanBuild%" == "1" set cleanBuild=-clean

echo Commencing CoreRT Repo build
echo.
setlocal
call "!VS%__VSProductVersion%COMNTOOLS!\..\..\VC\vcvarsall.bat" %__VCBuildArch%

call %~dp0run.cmd gen-buildsys

echo Commencing build of native components for %__BuildOS%.%__BuildArch%.%__BuildType%
echo.

echo call %~dp0run.cmd build-native %cleanBuild% -Platform=%__BuildArch% -Configuration=%__BuildType% 
call %~dp0run.cmd build-native %cleanBuild% -Platform=%__BuildArch% -Configuration=%__BuildType% 
endlocal

IF NOT ERRORLEVEL 1 goto ManagedBuild
echo Native component build failed. Refer !__NativeBuildLog! for details.
exit /b 1

:ManagedBuild
setlocal
echo Using CLI tools version:
dir /b "%__DotNetCliPath%\sdk"
call "!VS%__VSProductVersion%COMNTOOLS!\VsDevCmd.bat"
echo Commencing build of managed components for %__BuildOS%.%__BuildArch%.%__BuildType%
echo.
@call %~dp0run.cmd build-managed %cleanBuild% -RelativeProductBinDir=%__RelativeProductBinDir% -NuPkgRid=win7-x64 -RepoPath=%__ProjectDir%

endlocal
REM @call %~dp0run.cmd build-packages %*
REM @call %~dp0run.cmd build-tests %*

set "__BuildLog=%__LogsDir%\msbuild_%__BuildOS%__%__BuildArch%__%__BuildType%.log"
IF NOT ERRORLEVEL 1 (
  findstr /ir /c:".*Warning(s)" /c:".*Error(s)" /c:"Time Elapsed.*" "%__BuildLog%"
  goto AfterILCompilerBuild
)
echo ILCompiler build failed. Refer !__BuildLog! for details.
exit /b 1

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

:RunTests
if defined __SkipTests exit /b 0

pushd "%__ProjectDir%\tests"
call "runtest.cmd" %__BuildType% %__BuildArch% /dotnetclipath %__DotNetCliPath%
set TEST_EXIT_CODE=%ERRORLEVEL%
popd
exit /b %TEST_EXIT_CODE%

:Usage
echo.
echo Build the CoreRT repo.
echo.
echo Usage:
echo     %__ThisScriptShort% [option1] [option2] ...
echo.
echo All arguments are optional. The options are:
echo.
echo./? -? /h -h /help -help: view this message.
echo Build architecture: one of x64, x86, arm ^(default: x64^).
echo Build type: one of Debug, Checked, Release ^(default: Debug^).
echo Visual Studio version: ^(default: VS2015^).
echo clean: force a clean build ^(default is to perform an incremental build^).
echo skiptests: skip building tests ^(default: tests are built^).
exit /b 1

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
