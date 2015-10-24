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
goto :eof

:ArgsDone

set __BuildStr=%__BuildOS%.%__BuildArch%.%__BuildType%

set SCRIPT_DIR=%~dp0
set BIN_DIR=..\bin\tests
set PACKAGE=Microsoft.DotNet.ILToNative
set VERSION=1.0.0-prerelease

if /i "%__BuildArch%"=="x86" (
    set "CLANG_EXE=%PROGRAMFILES(x86)%\LLVM\bin\clang.exe"
) else (
    set "CLANG_EXE=%PROGRAMFILES%\LLVM\bin\clang.exe"
)

if /i "%__BuildType%"=="Debug" (
    set MSVCRT_LIB=msvcrtd.lib
) else (
    set MSVCRT_LIB=msvcrt.lib
)

set NUPKG_UNPACK_DIR=%BIN_DIR%\package
set NUPKG_INSTALL_DIR=%NUPKG_UNPACK_DIR%\install
set TOOLCHAIN_DIR=%NUPKG_INSTALL_DIR%\%PACKAGE%.%VERSION%
set NUPKG_PATH=..\bin\Product\%__BuildStr%\.nuget\%PACKAGE%.%VERSION%.nupkg
set NUGET_PATH=..\packages

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

echo.
echo Installing nuget package from %NUPKG_PATH% into %NUPKG_INSTALL_DIR%
echo ^<packages^>^<package id="%PACKAGE%" version="%VERSION%"/^>^</packages^> > %NUPKG_UNPACK_DIR%\packages.config
copy /y  %NUPKG_PATH% %NUPKG_UNPACK_DIR% > nul
%NUGET_PATH%\NuGet.exe install %NUPKG_UNPACK_DIR%\packages.config -Source %SCRIPT_DIR%%NUPKG_UNPACK_DIR% -OutputDir %NUPKG_INSTALL_DIR% -prerelease

echo.
if "%CORERT_EXT_PATH%"=="" set CORERT_EXT_PATH=..\..\corert.external
if "%PROTOJIT_PATH%"=="" set PROTOJIT_PATH=%CORERT_EXT_PATH%\Compiler\protojit.dll
if not exist "%PROTOJIT_PATH%" goto :NoCoreRTExt
if not exist "%CLANG_EXE%" goto :NoClang
if not exist "%TOOLCHAIN_DIR%\ILToNative.exe" goto :NoILToNative

echo Installing JIT from "%PROTOJIT_PATH%" to "%TOOLCHAIN_DIR%"
copy /y "%PROTOJIT_PATH%" "%TOOLCHAIN_DIR%" > nul

setlocal enabledelayedexpansion
set __VSProductVersion=140
set __VCBuildArch=x86_amd64
call "!VS%__VSProductVersion%COMNTOOLS!\..\..\VC\vcvarsall.bat" %__VCBuildArch%

set /a TOTAL_TESTS=0
set /a PASSED_TESTS=0
for /f "delims=" %%a in ('dir /s /aD /b *') do (
    set SOURCE_FILE=%%a\%%~na
    set SOURCE_FILENAME=%%~na
    if exist "!SOURCE_FILE!.cs" (
        call :CompileFile !SOURCE_FILE! !SOURCE_FILENAME!
        set /a TOTAL_TESTS=!TOTAL_TESTS!+1
    )
)

echo.
echo TOTAL: %TOTAL_TESTS% PASSED: %PASSED_TESTS%

endlocal
goto :eof

:NoILToNative
    echo ILToNative.exe not found at %TOOLCHAIN_DIR%, aborting test run.
    goto :eof

:NoClang
    echo clang.exe not found "%CLANG_EXE%", aborting test run.
    goto :eof

:NoCoreRTExt
    echo corert.external path not found %CORERT_EXT_PATH%, aborting test run.
    goto :eof

:NoNuPkg
    echo No nupkg at "%NUPKG_PATH%"
    goto :eof

:CompileFile
    echo.
    echo Compiling directory %~1
    set SOURCE_FILE=%~1
    set SOURCE_FILENAME=%~2
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
    %TOOLCHAIN_DIR%\ILToNative.exe -in !SOURCE_FILE!.exe -r %CORERT_EXT_PATH%\Runtime\*.dll -out !SOURCE_FILE!.S -r %TOOLCHAIN_DIR%\sdk\System.Private.Corelib.dll
    endlocal

    "%CLANG_EXE%" -c !SOURCE_FILE!.S -o !SOURCE_FILE!.obj

    link.exe  /ERRORREPORT:PROMPT !SOURCE_FILE!.obj /OUT:"!SOURCE_FILE!.compiled.exe" /NOLOGO kernel32.lib user32.lib gdi32.lib winspool.lib comdlg32.lib advapi32.lib shell32.lib ole32.lib oleaut32.lib uuid.lib odbc32.lib odbccp32.lib kernel32.lib user32.lib gdi32.lib winspool.lib comdlg32.lib advapi32.lib shell32.lib ole32.lib oleaut32.lib uuid.lib odbc32.lib odbccp32.lib /MANIFEST /MANIFESTUAC:"level='asInvoker' uiAccess='false'" /manifest:embed /SUBSYSTEM:CONSOLE /TLBID:1 /DYNAMICBASE /NXCOMPAT /IMPLIB:"!SOURCE_FILE!.lib" /MACHINE:X64 ..\bin\Product\%__BuildStr%\lib\Runtime.lib ..\bin\Product\%__BuildStr%\lib\reproNative.lib %MSVCRT_LIB% 

    echo.
    echo Running test !SOURCE_FILENAME!

    call !SOURCE_FILE!.cmd
    IF "%ERRORLEVEL%"=="0" (
        set /a PASSED_TESTS=%PASSED_TESTS%+1
    )
