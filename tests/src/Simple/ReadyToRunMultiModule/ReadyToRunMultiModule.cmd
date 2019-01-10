@echo off
setlocal

REM The arguments to invoke a ready-to-run test with ETW logging of jitted methods look like
REM dotnet run --project --corerun tests\src\tools\ReadyToRun.TestHarness bin\obj\<Build>\CoreCLRRuntime\CoreRun.exe --in bin\<cfg>\<arch>\native\<test>.ni.exe --whitelist methodWhiteList.txt
echo %3 %4 --corerun %5 --in "%1\%2" --whitelist %~dp0methodWhiteList.txt
call %3 %4 --corerun %5 --in "%1\%2" --whitelist %~dp0methodWhiteList.txt

IF "%errorlevel%"=="100" (
    echo %~n0: Pass
    EXIT /b 0
) ELSE (
    echo %~n0: fail - %ErrorLevel%
    EXIT /b 1
)
endlocal
