@echo off
setlocal

rem Enable Server GC for this test
set RH_UseServerGC=1
"%1\%2"
set ErrorCode=%ERRORLEVEL%
IF "%ErrorCode%"=="100" (
    echo %~n0: pass
    EXIT /b 0
) ELSE (
    echo %~n0: fail - %ErrorCode%
    EXIT /b 1
)
endlocal
