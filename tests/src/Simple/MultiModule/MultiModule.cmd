@echo off
setlocal
"%1\%2"
set ErrorCode=%ERRORLEVEL%
IF "%ErrorCode%"=="100" (
    echo %~n0: pass
    EXIT /b 0
) ELSE (
    echo %~n0: fail
    EXIT /b 1
)
endlocal
