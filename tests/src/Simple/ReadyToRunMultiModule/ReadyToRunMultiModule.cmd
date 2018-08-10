@echo off
setlocal

REM The arguments to invoke a ready-to-run test with ETW logging of jitted methods look like
REM dotnet run --project tests\src\tools\ReadyToRun.TestHarness bin\obj\<Build>\CoreCLRRuntime\CoreRun.exe bin\Debug\<arch>\native\<test>.ni.exe methodWhiteList.txt
echo %3 run --project %4 %5 "%1\%2" %~dp0methodWhiteList.txt
call %3 run --project %4 %5 "%1\%2" %~dp0methodWhiteList.txt

IF "%errorlevel%"=="100" (
    echo %~n0: Pass
    EXIT /b 0
) ELSE (
    echo %~n0: fail - %ErrorLevel%
    EXIT /b 1
)
endlocal
