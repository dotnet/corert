@echo off

call %~dp0testenv.cmd %*

set CoreRT_AppDepSdkPkg=toolchain.win7-%CoreRT_BuildArch%.Microsoft.DotNet.AppDep
set CoreRT_AppDepSdkVer=1.0.5-prerelease-00001

setlocal EnableExtensions

:Arg_Loop
if "%1" == "" goto ArgsDone
if /i "%1" == "/?" goto Usage
if /i "%1" == "/nugetexedir"   (set __NuGetExeDir=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/nugetopt"      (set __NuGetOptions=%2&shift&shift&goto Arg_Loop)

echo Invalid command line argument: %1
goto Usage
:ArgsDone

set __BuildStr=%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%
set __NuPkgInstallDir=%CoreRT_TestRoot%\..\bin\Product\%__BuildStr%\.nuget\publish1
if not exist %__NuGetExeDir%\NuGet.exe ((call :Fail "No NuGet.exe found at %__NuGetExeDir%. Specify /nugetexedir option") & exit /b -1)

REM ** Install packages from NuGet
set __NuGetFeedUrl="https://dotnet.myget.org/F/dotnet-corert/api/v2"

REM ** Install AppDep SDK
echo.
echo Installing CoreRT external dependencies
%__NuGetExeDir%\NuGet.exe install -Source %__NuGetFeedUrl% -OutputDir %__NuPkgInstallDir% -Version %CoreRT_AppDepSdkVer% %CoreRT_AppDepSdkPkg% -prerelease %__NuGetOptions%

endlocal & (
  set CoreRT_ToolchainDir=%__NuPkgInstallDir%
  set CoreRT_AppDepSdkDir=%__NuPkgInstallDir%\%CoreRT_AppDepSdkPkg%.%CoreRT_AppDepSdkVer%
)

exit /b 100

:Fail
    echo.
    powershell -Command Write-Host %1 -foreground "red"
    exit /b -1

:Usage
    echo Usage: %0 [dbg^|rel] [x86^|amd64^|arm] [/?]
    exit /b -8


