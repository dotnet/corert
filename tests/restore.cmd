@echo off

call %~dp0testenv.cmd %*

if not defined CoreRT_BuildOS set CoreRT_BuildOS=Windows_NT
if not defined CoreRT_BuildArch ((call :Fail "Set CoreRT_BuildArch to x86/x64/arm") & exit /b -1)
if not defined CoreRT_BuildType ((call :Fail "Set CoreRT_BuildType to Debug or Release") & exit /b -1)

set CoreRT_ToolchainPkg=toolchain.win7-%CoreRT_BuildArch%.Microsoft.DotNet.ILCompiler.Development
set CoreRT_ToolchainVer=1.0.0-prerelease
set CoreRT_AppDepSdkPkg=Microsoft.DotNet.AppDep
set CoreRT_AppDepSdkVer=1.0.0-prerelease
set CoreRT_ProtoJitPkg=Microsoft.DotNet.ProtoJit
set CoreRT_ProtoJitVer=1.0.0-prerelease
set CoreRT_ObjWriterPkg=Microsoft.DotNet.ObjectWriter
set CoreRT_ObjWriterVer=1.0.2-prerelease

setlocal EnableExtensions
set __ScriptDir=%~dp0
set __BuildStr=%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%

:Arg_Loop
if "%1" == "" goto ArgsDone
if /i "%1" == "/?" goto Usage
if /i "%1" == "/nugetexedir"   (set __NuGetExeDir=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/nupkgdir"      (set __BuiltNuPkgDir=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/installdir"    (set __NuPkgInstallDir=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/nugetopt"      (set __NuGetOptions=%2&shift&shift&goto Arg_Loop)

echo Invalid command line argument: %1
goto Usage
:ArgsDone

:SetTempFolder
set __TempFolder=%TMP%\unpack-%RANDOM%-%RANDOM%
if exist "%__TempFolder%" goto :SetTempFolder
set __NuPkgUnpackDir=%__TempFolder%

if not exist %__NuGetExeDir%\NuGet.exe ((call :Fail "No NuGet.exe found at %__NuGetExeDir%. Specify /nugetexedir option") & exit /b -1)
if "%__NuPkgInstallDir%"=="" ((call :Fail "Specify /installdir option") & exit /b -1)
if not exist "%__BuiltNuPkgDir%" ((call :Fail "Specify /nupkgdir to point to the built toolchain path") & exit /b -1)

REM ** Remove any old installed packages
echo Cleaning up %__NuPkgInstallDir%
rmdir /q /s %__NuPkgInstallDir% > NUL
mkdir %__NuPkgInstallDir%
if not exist %__NuPkgInstallDir% ((call :Fail "Could not make install dir") & exit /b -1)

REM ** Install packages from NuGet
set __NuGetFeedUrl="https://www.myget.org/F/schellap/auth/3e4f1dbe-f43a-45a8-b029-3ad4d25605ac/api/v2"

REM ** Install AppDep SDK
echo.
echo Installing CoreRT external dependencies
%__NuGetExeDir%\NuGet.exe install -Source %__NuGetFeedUrl% -OutputDir %__NuPkgInstallDir% -Version %CoreRT_AppDepSdkVer% %CoreRT_AppDepSdkPkg% -prerelease %__NuGetOptions%

REM ** Install ProtoJit
echo.
echo Installing ProtoJit from NuGet
%__NuGetExeDir%\NuGet.exe install -Source %__NuGetFeedUrl% -OutputDir %__NuPkgInstallDir% -Version %CoreRT_ProtoJitVer% %CoreRT_ProtoJitPkg% -prerelease %__NuGetOptions%

REM ** Install ObjectWriter
echo.
echo Installing ObjectWriter from NuGet
%__NuGetExeDir%\NuGet.exe install -Source %__NuGetFeedUrl% -OutputDir %__NuPkgInstallDir% -Version %CoreRT_ObjWriterVer% %CoreRT_ObjWriterPkg% -prerelease %__NuGetOptions%

REM ** Install the built toolchain from product dir
echo.
echo Installing ILCompiler from %__BuiltNuPkgPath% into %__NuPkgInstallDir%

set __BuiltNuPkgPath=%__BuiltNuPkgDir%\%CoreRT_ToolchainPkg%.%CoreRT_ToolchainVer%.nupkg
if not exist "%__BuiltNuPkgPath%" ((call :Fail "Did not find a built %__BuiltNuPkgPath%. Did you run build.cmd?") & exit /b -1)

mkdir %__NuPkgUnpackDir%
if not exist %__NuPkgUnpackDir% ((call :Fail "Could not make install dir") & exit /b -1)
copy /y %__BuiltNuPkgPath% %__NuPkgUnpackDir% > nul
echo ^<packages^>^<package id="%CoreRT_ToolchainPkg%" version="%CoreRT_ToolchainVer%"/^>^</packages^> > %__NuPkgUnpackDir%\packages.config
%__NuGetExeDir%\NuGet.exe install "%__NuPkgUnpackDir%\packages.config" -Source "%__NuPkgUnpackDir%" -OutputDir "%__NuPkgInstallDir%" -prerelease %__NuGetOptions%
rmdir /s /q %__NuPkgUnpackDir%

endlocal & (
  set CoreRT_AppDepSdkDir=%__NuPkgInstallDir%\%CoreRT_AppDepSdkPkg%.%CoreRT_AppDepSdkVer%
  set CoreRT_ProtoJitDir=%__NuPkgInstallDir%\%CoreRT_ProtoJitPkg%.%CoreRT_ProtoJitVer%
  set CoreRT_ObjWriterDir=%__NuPkgInstallDir%\%CoreRT_ObjWriterPkg%.%CoreRT_ObjWriterVer%
  set CoreRT_ToolchainDir=%__NuPkgInstallDir%\%CoreRT_ToolchainPkg%.%CoreRT_ToolchainVer%
)

exit /b 100

:Fail
    echo.
    powershell -Command Write-Host %1 -foreground "red"
    exit /b -1

:Usage
    echo Usage: %0 [dbg^|rel] [x86^|amd64^|arm] [/?]
    exit /b -8


