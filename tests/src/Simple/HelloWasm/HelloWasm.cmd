@echo off
setlocal

call %EMSDK_NODE% "%1\%2" 

IF "%errorlevel%"=="100" (
    echo %~n0: Pass
    EXIT /b 0
) ELSE (
    echo %~n0: fail - %ErrorLevel%
    EXIT /b 1
)
endlocal

