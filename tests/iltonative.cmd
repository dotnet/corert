@echo off

setlocal

REM ** Validate args
set "__SourceFolder=%1" & shift
set "__SourceFile=%__SourceFolder%\%1" & shift

set __ExeParams=
:Loop
    if [%1]==[] goto :DoneArgs
    set __ExeParams=%__ExeParams% %1
    shift
goto :Loop

:DoneArgs

if not exist "%__SourceFolder%" goto :InvalidArgs
if not exist "%__SourceFile%" goto :InvalidArgs

REM ** Build variables
if not defined CoreRT_BuildOS set CoreRT_BuildOS=Windows_NT
if not defined CoreRT_BuildArch ((call :Fail "Set CoreRT_BuildArch to x86/x64/arm") & exit /b -1)
if not defined CoreRT_BuildType ((call :Fail "Set CoreRT_BuildType to Debug or Release") & exit /b -1)

if /i "%CoreRT_BuildType%"=="Debug" (
    set __LinkLibs=msvcrtd.lib
) else (
    set __LinkLibs=msvcrt.lib
)
set __BuildStr=%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%

if not exist "%CoreRT_ToolchainDir%\dotnet-compile-native.bat" ((call :Fail "Toolchain not installed correctly at CoreRT_ToolchainDir") & exit /b -1)

if "%CoreRT_TestCompileMode%"=="" ((call :Fail "Test compile mode not set in CoreRT_TestCompileMode: Specify cpp/protojit") & exit /b -1)

set "__CompileLogPath=%__SourceFolder%"

REM ** Invoke ILToNative to compile. Set CORE_ROOT to empty so, ILToNative's corerun doesn't load dependencies from there.
set CORE_ROOT=
call %CoreRT_ToolchainDir%\dotnet-compile-native.bat %CoreRT_BuildArch% %CoreRT_BuildType% /mode %CoreRT_TestCompileMode% /appdepsdk %CoreRT_AppDepSdkDir% /codegenpath %CoreRT_ProtoJitDir% /objgenpath %CoreRT_ObjWriterDir% /logpath %__CompileLogPath% /linklibs %__LinkLibs% /in %__SourceFile% /out %__SourceFile%.compiled.exe

REM ** Fail if we did not generate obj file
if exist "%__SourceFile%.compiled.exe" (
    REM ** Should run the tests?
    if "%CoreRT_TestRun%"=="false" (exit /b 100)
    
    REM ** Run the test
    "%__SourceFile%.compiled.exe" %__ExeParams%

    exit /b !ErrorLevel!
) else (
    echo "ILToNative failed to generate exe, exiting..."
    exit /b -1
)

endlocal

:Fail
echo.
powershell -Command Write-Host %1 -foreground "red"
exit /b -1

:InvalidArgs
echo.
echo Usage: %0 ^<exe-dir^> ^<exe-name^> ^<exe-args^>
exit /b -3
