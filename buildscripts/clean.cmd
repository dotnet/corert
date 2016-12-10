@if not defined _echo @echo off
setlocal EnableDelayedExpansion

echo Stop VBCSCompiler.exe execution.
for /f "tokens=2 delims=," %%F in ('tasklist /nh /fi "imagename eq VBCSCompiler.exe" /fo csv') do taskkill /f /PID %%~F

echo Cleaning entire working directory ...
call git clean -xdf
exit /b !ERRORLEVEL!
