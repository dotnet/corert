@echo off
setlocal

set __BuildArch=x64
set __BuildType=Debug
set __BuildOS=Windows_NT

:Arg_Loop
if "%1" == "" goto ArgsDone
if /i "%1" == "/?" goto Usage
if /i "%1" == "x64"    (set __BuildArch=x64&&shift&goto Arg_Loop)
if /i "%1" == "x86"    (set __BuildArch=x86&&shift&goto Arg_Loop)
if /i "%1" == "arm"    (set __BuildArch=arm&&shift&goto Arg_Loop)

if /i "%1" == "debug"    (set __BuildType=Debug&shift&goto Arg_Loop)
if /i "%1" == "release"  (set __BuildType=Release&shift&goto Arg_Loop)

echo Invalid command line argument: %1
goto Usage

:Usage
echo %0 [OS] [arch] [flavor]
exit /b 2

:ArgsDone

set __BuildStr=%__BuildOS%.%__BuildArch%.%__BuildType%

set SCRIPT_DIR=%~dp0
set BIN_DIR=%SCRIPT_DIR%..\bin\tests
set PACKAGE=toolchain.%__BuildOS%-%__BuildArch%.Microsoft.DotNet.ILToNative.Development
set VERSION=1.0.0-prerelease

if /i "%__BuildType%"=="Debug" (
    set MSVCRT_LIB=msvcrtd.lib
) else (
    set MSVCRT_LIB=msvcrt.lib
)

set NUPKG_UNPACK_DIR=%BIN_DIR%\package
set NUPKG_INSTALL_DIR=%NUPKG_UNPACK_DIR%\install
set TOOLCHAIN_DIR=%NUPKG_INSTALL_DIR%\%PACKAGE%.%VERSION%
set NUPKG_PATH=%SCRIPT_DIR%..\bin\Product\%__BuildStr%\.nuget\%PACKAGE%.%VERSION%.nupkg
set NUGET_PATH=%SCRIPT_DIR%..\packages

echo.
echo -------------------------
echo BEGIN TEST EXECUTION
echo -------------------------

echo Cleaning up %NUPKG_INSTALL_DIR%
rmdir /q /s %NUPKG_INSTALL_DIR%
setlocal enableextensions
mkdir %NUPKG_INSTALL_DIR%
endlocal

if not exist "%NUPKG_PATH%" goto :NoNuPkg

set NUGET_FEED_URL="https://www.myget.org/F/schellap/auth/3e4f1dbe-f43a-45a8-b029-3ad4d25605ac/api/v2"

echo.
echo Installing CoreRT external dependencies
set CORERT_EXT_PACKAGE=Microsoft.DotNet.AppDep
set CORERT_EXT_VERSION=1.0.0-prerelease
%NUGET_PATH%\NuGet.exe install -Source %NUGET_FEED_URL% -OutputDir %NUPKG_INSTALL_DIR% -Version %CORERT_EXT_VERSION% %CORERT_EXT_PACKAGE% -prerelease

echo.
echo Installing ProtoJit from NuGet.
set PROTOJIT_PACKAGE=Microsoft.DotNet.ProtoJit
set PROTOJIT_VERSION=1.0.0-prerelease
%NUGET_PATH%\NuGet.exe install -Source %NUGET_FEED_URL% -OutputDir %NUPKG_INSTALL_DIR% -Version %PROTOJIT_VERSION% %PROTOJIT_PACKAGE% -prerelease

echo.
echo Installing ObjectWriter from NuGet.
set OBJWRITER_PACKAGE=Microsoft.DotNet.ObjectWriter
set OBJWRITER_VERSION=1.0.1-prerelease
%NUGET_PATH%\NuGet.exe install -Source %NUGET_FEED_URL% -OutputDir %NUPKG_INSTALL_DIR% -Version %OBJWRITER_VERSION% %OBJWRITER_PACKAGE% -prerelease

echo.
echo Installing ILToNative from %NUPKG_PATH% into %NUPKG_INSTALL_DIR%.
echo ^<packages^>^<package id="%PACKAGE%" version="%VERSION%"/^>^</packages^> > %NUPKG_UNPACK_DIR%\packages.config
copy /y  %NUPKG_PATH% %NUPKG_UNPACK_DIR% > nul
%NUGET_PATH%\NuGet.exe install %NUPKG_UNPACK_DIR%\packages.config -Source %NUPKG_UNPACK_DIR% -OutputDir %NUPKG_INSTALL_DIR% -prerelease

echo.
set CORERT_EXT_PATH=%NUPKG_INSTALL_DIR%\%CORERT_EXT_PACKAGE%.%CORERT_EXT_VERSION%
set PROTOJIT_PATH=%NUPKG_INSTALL_DIR%\%PROTOJIT_PACKAGE%.%PROTOJIT_VERSION%
set OBJWRITER_PATH=%NUPKG_INSTALL_DIR%\%OBJWRITER_PACKAGE%.%OBJWRITER_VERSION%

if not exist "%CORERT_EXT_PATH%" set CORERT_EXT_PATH=..\..\corert.external

if not exist "%PROTOJIT_PATH%" set PROTOJIT_PATH=%CORERT_EXT_PATH%\Compiler\protojit.dll
if not exist "%PROTOJIT_PATH%" set PROTOJIT_PATH=%CORERT_EXT_PATH%\protojit.dll

if not exist "%OBJWRITER_PATH%" set OBJWRITER_PATH=%CORERT_EXT_PATH%\Compiler\objwriter.dll
if not exist "%OBJWRITER_PATH%" set OBJWRITER_PATH=%CORERT_EXT_PATH%\objwriter.dll

set CORERT_EXT_RUNTIME=%CORERT_EXT_PATH%\Runtime
if not exist "%CORERT_EXT_RUNTIME%" set CORERT_EXT_RUNTIME=%CORERT_EXT_PATH%

if not exist "%CORERT_EXT_RUNTIME%" goto :NoCoreRTExt
if not exist "%PROTOJIT_PATH%" goto :NoCoreRTExt
if not exist "%OBJWRITER_PATH%" goto :NoCoreRTExt

if not exist "%TOOLCHAIN_DIR%\ILToNative.exe" goto :NoILToNative

setlocal enabledelayedexpansion
set __VSProductVersion=140
set __VCBuildArch=x86_amd64
call "!VS%__VSProductVersion%COMNTOOLS!\..\..\VC\vcvarsall.bat" %__VCBuildArch%

echo. > %BIN_DIR%\testResults.tmp
set /a TOTAL_TESTS=0
set /a PASSED_TESTS=0
for /f "delims=" %%a in ('dir /s /aD /b *') do (
    set SOURCE_FOLDER=%%a
    set SOURCE_FILENAME=%%~na
    if exist "!SOURCE_FOLDER!\!SOURCE_FILENAME!.cs" (
        call :CompileFile !SOURCE_FOLDER! !SOURCE_FILENAME!
        set /a TOTAL_TESTS=!TOTAL_TESTS!+1
    )
)

set /a FAILED_TESTS=%TOTAL_TESTS%-%PASSED_TESTS%
echo.

echo ^<?xml version="1.0" encoding="utf-8"?^> > %BIN_DIR%\testResults.xml
echo ^<assemblies^>  >> %BIN_DIR%\testResults.xml
echo ^<assembly name="ILToNative" total="%TOTAL_TESTS%" passed="%PASSED_TESTS%" failed="%FAILED_TESTS%" skipped="0"^>  >> %BIN_DIR%\testResults.xml
echo ^<collection total="%TOTAL_TESTS%" passed="%PASSED_TESTS%" failed="%FAILED_TESTS%" skipped="0"^>  >> %BIN_DIR%\testResults.xml
type %BIN_DIR%\testResults.tmp >> %BIN_DIR%\testResults.xml
echo ^</collection^>  >> %BIN_DIR%\testResults.xml
echo ^</assembly^>  >> %BIN_DIR%\testResults.xml
echo ^</assemblies^>  >> %BIN_DIR%\testResults.xml

echo.
set "OUTSTR=TOTAL: %TOTAL_TESTS% PASSED: %PASSED_TESTS%"
if %TOTAL_TESTS% EQU %PASSED_TESTS% (
    powershell -Command Write-Host "%OUTSTR%" -foreground "Black" -background "Green"
    exit /b 0
) else ( 
    powershell -Command Write-Host "%OUTSTR%" -foreground "White" -background "Red"
    exit /b 1
)

:NoILToNative
    echo ILToNative.exe not found at %TOOLCHAIN_DIR%, aborting test run.
    exit /b 3

:NoCoreRTExt
    echo corert.external path not found %CORERT_EXT_PATH%, aborting test run.
    echo set environment variable CORERT_EXT_PATH to the latest corert external path.
    exit /b 4

:NoNuPkg
    echo No nupkg at "%NUPKG_PATH%"
    exit /b 5

:CompileFile
    echo.
    echo Compiling directory %~1
    set SOURCE_FOLDER=%~1
    set SOURCE_FILENAME=%~2
    set SOURCE_FILE=!SOURCE_FOLDER!\!SOURCE_FILENAME!
    if exist "!SOURCE_FILE!.S" del "!SOURCE_FILE!.S"
    if exist "!SOURCE_FILE!.compiled.exe" del "!SOURCE_FILE!.compiled.exe"
    if exist "!SOURCE_FILE!.obj" del "!SOURCE_FILE!.obj"
    if exist "!SOURCE_FILE!.exe" del "!SOURCE_FILE!.exe"

    setlocal
    echo.
    echo Begin managed build of !SOURCE_FILE!.cs
    call "!VS%__VSProductVersion%COMNTOOLS!\VsDevCmd.bat"
    csc.exe /nologo /noconfig /unsafe+ /nowarn:1701,1702,2008 /langversion:5 /nostdlib+ /errorreport:prompt /warn:4 /define:TRACE;DEBUG;SIGNED /errorendlocation /preferreduilang:en-US /reference:..\packages\System.Collections\4.0.0\ref\dotnet\System.Collections.dll /reference:..\packages\System.Console\4.0.0-beta-23419\ref\dotnet\System.Console.dll /reference:..\packages\System.Diagnostics.Debug\4.0.0\ref\dotnet\System.Diagnostics.Debug.dll /reference:..\packages\System.Globalization\4.0.0\ref\dotnet\System.Globalization.dll /reference:..\packages\System.IO\4.0.10\ref\dotnet\System.IO.dll /reference:..\packages\System.IO.FileSystem\4.0.0\ref\dotnet\System.IO.FileSystem.dll /reference:..\packages\System.IO.FileSystem.Primitives\4.0.0\ref\dotnet\System.IO.FileSystem.Primitives.dll /reference:..\packages\System.Reflection\4.0.0\ref\dotnet\System.Reflection.dll /reference:..\packages\System.Reflection.Extensions\4.0.0\ref\dotnet\System.Reflection.Extensions.dll /reference:..\packages\System.Reflection.Primitives\4.0.0\ref\dotnet\System.Reflection.Primitives.dll /reference:..\packages\System.Resources.ResourceManager\4.0.0\ref\dotnet\System.Resources.ResourceManager.dll /reference:..\packages\System.Runtime\4.0.20\ref\dotnet\System.Runtime.dll /reference:..\packages\System.Runtime.Extensions\4.0.10\ref\dotnet\System.Runtime.Extensions.dll /reference:..\packages\System.Runtime.Handles\4.0.0\ref\dotnet\System.Runtime.Handles.dll /reference:..\packages\System.Runtime.InteropServices\4.0.10\ref\dotnet\System.Runtime.InteropServices.dll /reference:..\packages\System.Text.Encoding\4.0.0\ref\dotnet\System.Text.Encoding.dll /reference:..\packages\System.Text.Encoding.Extensions\4.0.0\ref\dotnet\System.Text.Encoding.Extensions.dll /reference:..\packages\System.Threading\4.0.0\ref\dotnet\System.Threading.dll /reference:..\packages\System.Threading.Overlapped\4.0.0\ref\dotnet\System.Threading.Overlapped.dll /reference:..\packages\System.Threading.Tasks\4.0.10\ref\dotnet\System.Threading.Tasks.dll /debug+ /debug:full /filealign:512 /optimize- /out:!SOURCE_FILE!.exe /target:exe /warnaserror+ /utf8output !SOURCE_FILE!.cs

    echo.
    echo Compiling ILToNative !SOURCE_FILE!.exe
    call %TOOLCHAIN_DIR%\dotnet-compile-native.bat %__BuildArch% %__BuildType% /mode protojit /appdepsdk %CORERT_EXT_PATH% /codegenpath %PROTOJIT_PATH% /objgenpath %OBJWRITER_PATH% /logpath %SOURCE_FOLDER% /linklibs %MSVCRT_LIB% /in !SOURCE_FILE!.exe /out !SOURCE_FILE!.compiled.exe
    endlocal
    IF "%ERRORLEVEL%" == "0" (
        echo.
        echo Running test !SOURCE_FILENAME!
        call !SOURCE_FILE!.cmd
    )
    
    IF "%ERRORLEVEL%" == "0" (
        set /a PASSED_TESTS=%PASSED_TESTS%+1
        echo ^<test name="!SOURCE_FILE!" type="Program" method="Main" result="Pass" /^> >> %BIN_DIR%\testResults.tmp
    ) ELSE (
        echo ^<test name="!SOURCE_FILE!" type="Program" method="Main" result="Fail"^> >> %BIN_DIR%\testResults.tmp
        echo ^<failure exception-type="Exit code: %ERRORLEVEL%" ^> >> %BIN_DIR%\testResults.tmp
        echo     ^<message^>See !SOURCE_FILE!.*.log ^</message^> >> %BIN_DIR%\testResults.tmp
        echo ^</failure^> >> %BIN_DIR%\testResults.tmp
        echo ^</test^> >> %BIN_DIR%\testResults.tmp
    )
