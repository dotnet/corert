@echo off
setlocal
set ErrorCode=100
for /f "usebackq delims=;" %%F in (`%~dp0\%~n0.compiled.exe`) do (
    if "%%F"=="Hello world" set ErrorCode=0
)
IF "%ErrorCode%"=="0" (
    echo %~n0: pass
    EXIT /b 0
) ELSE (
    echo %~n0: fail
    EXIT /b 1
)
endlocal

