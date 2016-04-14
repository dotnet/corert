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

if "%CoreRT_TestCompileMode%"=="" ((call :Fail "Test compile mode not set in CoreRT_TestCompileMode: Specify cpp/ryujit") & exit /b -1)
set __RSPTemplate="%CoreRT_RspTemplateDir%\%CoreRT_TestCompileMode%.rsp"
if not exist %__RSPTemplate% ((call :Fail "RSP template not found (%__RSPTemplate%)") & exit /b -1)

set __RSPFile=%__SourceFile%.rsp

rem Copy the relevant lines from the template RSP file
findstr /B /C:"-r:" %__RSPTemplate% > %__RSPFile%

echo %__SourceFile% >> %__RSPFile%
if exist "%__SourceFolder%\*.dll" (
    echo -r:%__SourceFolder%\*.dll >> %__RSPFile%
)

REM Initialize environment to invoke native tools
call "%VS140COMNTOOLS%\..\..\VC\vcvarsall.bat" amd64

set __ExeFile=%__SourceFile%.compiled.exe
if exist %__ExeFile% del %__ExeFile%

REM ** Set CORE_ROOT to empty so, ILCompiler's corerun doesn't load dependencies from there.
set CORE_ROOT=

if /i "%CoreRT_TestCompileMode%" == "cpp" goto :ModeCPP
if /i "%CoreRT_TestCompileMode%" == "ryujit" goto :ModeRyuJIT
echo Unrecognized compile mode: %CoreRT_TestCompileMode%
exit /b -1

:ModeRyuJIT
set __ObjFile=%__SourceFile%.obj
if exist %__ObjFile% del %__ObjFile%

echo -o:%__ObjFile% >> %__RSPFile%

%CoreRT_ToolchainDir%\corerun.exe %CoreRT_ToolchainDir%\ilc.exe @%__RSPFile%
if %ERRORLEVEL% NEQ 0 ((call :Fail "Unable to generate object file") & exit /b -1)

link %__ObjFile% %CoreRT_ToolchainDir%\sdk\bootstrapper.lib %CoreRT_ToolchainDir%\sdk\runtime.lib ole32.lib OleAut32.lib kernel32.lib /out:%__ExeFile% /debug:full
if %ERRORLEVEL% NEQ 0 ((call :Fail "Linking failed") & exit /b -1)

goto :Run

:ModeCPP
call :Fail "ModeCPP Not Yet Implemented"
exit /b -1

:Run
REM ** Fail if we did not generate obj file
if exist "%__ExeFile%" (
    REM ** Should run the tests?
    if "%CoreRT_TestRun%"=="false" (exit /b 100)
    
    REM ** Run the test
    "%__ExeFile%" %__ExeParams%

    exit /b !ErrorLevel!
) else (
    echo "ILCompiler failed to generate exe, exiting..."
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
