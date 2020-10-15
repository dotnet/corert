@echo off
setlocal

IF /i "%__Mode%"=="wasm" (
    call %EMSDK_NODE% "%1\%2"
) ELSE (
    "%1\%2"
)

set ErrorCode=%ERRORLEVEL%
IF "%ErrorCode%"=="100" (
    echo %~n0: pass
    EXIT /b 0
) ELSE (
    echo %~n0: fail - %ErrorCode%
    EXIT /b 1
)
endlocal
