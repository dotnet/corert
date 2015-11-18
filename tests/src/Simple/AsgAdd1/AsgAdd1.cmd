@echo off
setlocal
%~dp0\bin\%1\dnxcore50\native\%~n0.exe
set ErrorCode=%ERRORLEVEL%
IF "%ErrorCode%"=="100" (
    echo %~n0: pass
    EXIT /b 0
) ELSE (
    echo %~n0: fail
    EXIT /b 1
)
endlocal

