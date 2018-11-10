@echo off
setlocal

call emrun --browser=firefox --browser_args=-headless --safe_firefox_profile --silence_timeout 100 "%1\%2" 

IF "%errorlevel%"=="100" (
    echo %~n0: Pass
    EXIT /b 0
) ELSE (
    echo %~n0: fail - %ErrorLevel%
    EXIT /b 1
)
endlocal

