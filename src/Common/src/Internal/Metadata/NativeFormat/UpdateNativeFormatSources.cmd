@echo off

setlocal

if "%PYTHON%"=="" (
  echo PYTHON environment variable not set. Point this to a Python interpreter EXE (e.g. ipy.exe for IronPython^)
  exit /b 1
)

set __py=%PYTHON% -W "ignore::DeprecationWarning::"

echo Generating public APIs.
%__py% Script\CsPublicGen2.py
if ERRORLEVEL 1 goto Fail

echo Generating native format reader sources.
%__py% Script\CsReaderGen2.py
if ERRORLEVEL 1 goto Fail

echo Generating native format writer sources.
%__py% Script\CsWriterGen2.py
if ERRORLEVEL 1 goto Fail

echo Generating binary format reader and writer sources.
%__py% Script\CsMdBinaryRWCommonGen.py
if ERRORLEVEL 1 goto Fail

goto :EOF

:Fail
echo ERROR: Failed running script
exit /b 1
