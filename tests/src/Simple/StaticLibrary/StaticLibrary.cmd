@echo off
setlocal
"%1\StaticLibraryTest"
set ErrorCode=%ERRORLEVEL%
IF "%ErrorCode%"=="100" (
    echo %~n0: pass
    EXIT /b 0
) ELSE (
    echo %~n0: fail - %ErrorCode%
    EXIT /b 1
)
endlocal
