@echo off
REM don't pass args to buildvars-setup, just get defaults
call %~dp0buildvars-setup.cmd

set _msbuildexe="%ProgramFiles(x86)%\MSBuild\14.0\Bin\MSBuild.exe"
if not exist %_msbuildexe% (set _msbuildexe="%ProgramFiles%\MSBuild\14.0\Bin\MSBuild.exe")
REM hopefully it's on the path
if not exist %_msbuildexe% set _msbuildexe=msbuild

set AzureAccount=
set AzureToken=
set Container=

:Arg_Loop
if "%1" == "" goto ArgsDone

if /i "%1" == "-AzureAccount" (set AzureAccount=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "-AzureToken" (set AzureToken=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "-Container" (set Container=%2&shift&shift&goto Arg_Loop)

echo Invalid command line argument: %1
exit /b 1
:ArgsDone

set AzureToken=%AzureToken:"=%

if "%AzureAccount%" == "" (
    echo Azure account not specified.
    exit /b 1
)
if "%AzureToken%" == "" (
    echo Azure token not specified.
    exit /b 1
)
if "%Container%" == "" (
    echo Azure container not specified.
    exit /b 1
)

%_msbuildexe% %__ProjectDir%\buildscripts\publish.proj /p:CloudDropAccountName=%AzureAccount% /p:CloudDropAccessToken=%AzureToken% /p:ContainerName=%Container% /flp:v=diag;LogFile=publish-packages.log