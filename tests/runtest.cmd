@echo off

set CoreRT_TestRoot=%~dp0
set CoreRT_BuildArch=x64
set CoreRT_BuildType=Debug
set CoreRT_BuildOS=Windows_NT
set CoreRT_TestRun=true
set CoreRT_TestCompileMode=protojit
set CoreRT_TestExtRepo=
set CoreRT_BuildExtRepo=

:ArgLoop
if "%1" == "" goto :ArgsDone
if /i "%1" == "/?" goto :Usage
if /i "%1" == "x64"    (set CoreRT_BuildArch=x64&&shift&goto ArgLoop)
if /i "%1" == "x86"    (set CoreRT_BuildArch=x86&&shift&goto ArgLoop)
if /i "%1" == "arm"    (set CoreRT_BuildArch=arm&&shift&goto ArgLoop)

if /i "%1" == "debug"    (set CoreRT_BuildType=Debug&shift&goto ArgLoop)
if /i "%1" == "release"  (set CoreRT_BuildType=Release&shift&goto ArgLoop)
if /i "%1" == "/extrepo"  (set CoreRT_TestExtRepo=%2&shift&shift&goto ArgLoop)
if /i "%1" == "/buildextrepo" (set CoreRT_BuildExtRepo=%2&shift&shift&goto ArgLoop)
if /i "%1" == "/mode" (set CoreRT_TestCompileMode=%2&shift&shift&goto ArgLoop)
if /i "%1" == "/runtest" (set CoreRT_TestRun=%2&shift&shift&goto ArgLoop)
if /i "%1" == "/nocache" (set CoreRT_NuGetOptions=-nocache&shift&goto ArgLoop)

echo Invalid command line argument: %1
goto :Usage

:Usage
echo %0 [OS] [arch] [flavor] [/extrepo] [/buildextrepo] [/mode] [/runtest]
echo     /mode         : Compilation mode. Specify cpp/protojit. Default: protojit
echo     /runtest      : Should just compile or run compiled bianry? Specify: true/false. Default: true.
echo     /extrepo      : Path to external repo, currently supports: GitHub: dotnet/coreclr. Specify full path. If unspecified, runs corert tests
echo     /buildextrepo : Should build at root level of external repo? Specify: true/false. Default: true
echo     /nocache      : When restoring toolchain packages, obtain them from the feed not the cache.
exit /b 2

:ArgsDone

setlocal EnableDelayedExpansion
set __BuildStr=%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%
set __BinDir=%CoreRT_TestRoot%..\bin\tests
set __LogDir=%CoreRT_TestRoot%\..\bin\Logs\%__BuildStr%\tests
set __NuPkgInstallDir=%__BinDir%\package
set __BuiltNuPkgDir=%CoreRT_TestRoot%..\bin\Product\%__BuildStr%\.nuget

set __PackageRestoreCmd=restore.cmd
call %__PackageRestoreCmd% /nugetexedir %CoreRT_TestRoot%..\packages /installdir %__NuPkgInstallDir% /nupkgdir %__BuiltNuPkgDir% /nugetopt %CoreRT_NuGetOptions%
if not "%ErrorLevel%"=="100" (((call :Fail "Preptests failed... cannot continue")) & exit /b -1)

REM ** Validate the paths needed to run tests
if not exist "%CoreRT_AppDepSdkDir%" ((call :Fail "AppDep SDK not installed at %CoreRT_AppDepSdkDir%") & exit /b -1)
if not exist "%CoreRT_ProtoJitDir%"  ((call :Fail "ProtoJIT not installed at %CoreRT_ProtoJitDir%") & exit /b -1)
if not exist "%CoreRT_ObjWriterDir%" ((call :Fail "ObjWriter not installed at %ObjWriterDir%") & exit /b -1)
if not exist "%CoreRT_ToolchainDir%\dotnet-compile-native.bat" ((call :Fail "dotnet-compile-native.bat not found in %CoreRT_ToolchainDir%") & exit /b -1)

if not "%CoreRT_TestExtRepo%"=="" goto :TestExtRepo

if not defined CoreRT_ToolchainPkg ((call :Fail "Run %__PackageRestoreCmd% first") & exit /b -1)
if not defined CoreRT_ToolchainVer ((call :Fail "Run %__PackageRestoreCmd% first") & exit /b -1)

if /i "%__BuildType%"=="Debug" (
    set __LinkLibs=msvcrtd.lib
) else (
    set __LinkLibs=msvcrt.lib
)

echo. > %__BinDir%\testResults.tmp

set /a __TotalTests=0
set /a __PassedTests=0
for /f "delims=" %%a in ('dir /s /aD /b src\*') do (
    set __SourceFolder=%%a
    set __SourceFileName=%%~na
    set __RelativePath=!__SourceFolder:%CoreRT_TestRoot%=!
    if exist "!__SourceFolder!\!__SourceFileName!.cs" (
        call :CompileFile !__SourceFolder! !__SourceFileName! %__LogDir%\!__RelativePath!
        set /a __TotalTests=!__TotalTests!+1
    ) else (echo !__SourceFolder!\!__SourceFileName!)
)
set /a __FailedTests=%__TotalTests%-%__PassedTests%

echo ^<?xml version="1.0" encoding="utf-8"?^> > %__BinDir%\testResults.xml
echo ^<assemblies^>  >> %__BinDir%\testResults.xml
echo ^<assembly name="ILCompiler" total="%__TotalTests%" passed="%__PassedTests%" failed="%__FailedTests%" skipped="0"^>  >> %__BinDir%\testResults.xml
echo ^<collection total="%__TotalTests%" passed="%__PassedTests%" failed="%__FailedTests%" skipped="0"^>  >> %__BinDir%\testResults.xml
type %__BinDir%\testResults.tmp >> %__BinDir%\testResults.xml
echo ^</collection^>  >> %__BinDir%\testResults.xml
echo ^</assembly^>  >> %__BinDir%\testResults.xml
echo ^</assemblies^>  >> %__BinDir%\testResults.xml

echo.
set "__ConsoleOut=TOTAL: %__TotalTests% PASSED: %__PassedTests%"

if %__TotalTests% EQU %__PassedTests% (set __StatusPassed=1)
if %__TotalTests% EQU 0 (set __StatusPassed=0)
if "%__StatusPassed%"=="1" (
    powershell -Command Write-Host "%__ConsoleOut%" -foreground "Black" -background "Green"
    exit /b 0
) else ( 
    powershell -Command Write-Host "%__ConsoleOut%" -foreground "White" -background "Red"
    exit /b 1
)

:CompileFile
    echo.
    echo Compiling directory %~1
    set __SourceFolder=%~1
    set __SourceFileName=%~2
    set __CompileLogPath=%~3
    if not exist "!__CompileLogPath!" (mkdir !__CompileLogPath!)
    endlocal
    set __SourceFile=!__SourceFolder!\!__SourceFileName!
    call :DeleteFile "!__SourceFile!.S"
    call :DeleteFile "!__SourceFile!.compiled.exe"
    call :DeleteFile "!__SourceFile!.obj"
    call :DeleteFile "!__SourceFile!.exe"

    set __VSProductVersion=140
    set __VCBuildArch=x86_amd64
    setlocal
    echo.
    echo Begin managed build of !__SourceFile!.cs
    call "!VS%__VSProductVersion%COMNTOOLS!\VsDevCmd.bat"
    csc.exe /nologo /noconfig /unsafe+ /nowarn:1701,1702,2008 /langversion:5 /nostdlib+ /errorreport:prompt /warn:4 /define:TRACE;DEBUG;SIGNED /errorendlocation /preferreduilang:en-US /reference:..\packages\System.Collections\4.0.0\ref\dotnet\System.Collections.dll /reference:..\packages\System.Console\4.0.0-beta-23419\ref\dotnet\System.Console.dll /reference:..\packages\System.Diagnostics.Debug\4.0.0\ref\dotnet\System.Diagnostics.Debug.dll /reference:..\packages\System.Globalization\4.0.0\ref\dotnet\System.Globalization.dll /reference:..\packages\System.IO\4.0.10\ref\dotnet\System.IO.dll /reference:..\packages\System.IO.FileSystem\4.0.0\ref\dotnet\System.IO.FileSystem.dll /reference:..\packages\System.IO.FileSystem.Primitives\4.0.0\ref\dotnet\System.IO.FileSystem.Primitives.dll /reference:..\packages\System.Reflection\4.0.0\ref\dotnet\System.Reflection.dll /reference:..\packages\System.Reflection.Extensions\4.0.0\ref\dotnet\System.Reflection.Extensions.dll /reference:..\packages\System.Reflection.Primitives\4.0.0\ref\dotnet\System.Reflection.Primitives.dll /reference:..\packages\System.Resources.ResourceManager\4.0.0\ref\dotnet\System.Resources.ResourceManager.dll /reference:..\packages\System.Runtime\4.0.20\ref\dotnet\System.Runtime.dll /reference:..\packages\System.Runtime.Extensions\4.0.10\ref\dotnet\System.Runtime.Extensions.dll /reference:..\packages\System.Runtime.Handles\4.0.0\ref\dotnet\System.Runtime.Handles.dll /reference:..\packages\System.Runtime.InteropServices\4.0.10\ref\dotnet\System.Runtime.InteropServices.dll /reference:..\packages\System.Text.Encoding\4.0.0\ref\dotnet\System.Text.Encoding.dll /reference:..\packages\System.Text.Encoding.Extensions\4.0.0\ref\dotnet\System.Text.Encoding.Extensions.dll /reference:..\packages\System.Threading\4.0.0\ref\dotnet\System.Threading.dll /reference:..\packages\System.Threading.Overlapped\4.0.0\ref\dotnet\System.Threading.Overlapped.dll /reference:..\packages\System.Threading.Tasks\4.0.10\ref\dotnet\System.Threading.Tasks.dll /debug+ /debug:full /filealign:512 /optimize- /out:!__SourceFile!.exe /target:exe /warnaserror+ /utf8output !__SourceFile!.cs

    echo.
    echo Compiling ILCompiler !__SourceFile!.exe
    call !CoreRT_ToolchainDir!\dotnet-compile-native.bat %__BuildArch% %__BuildType% /mode %CoreRT_TestCompileMode% /appdepsdk %CoreRT_AppDepSdkDir% /codegenpath %CoreRT_ProtoJitDir% /objgenpath %CoreRT_ObjWriterDir% /logpath %__CompileLogPath% /linklibs %__LinkLibs% /in !__SourceFile!.exe /out !__SourceFile!.compiled.exe
    endlocal

    set __SavedErrorLevel=%ErrorLevel%
    if "%CoreRT_TestRun%"=="false" (goto :SkipTestRun)

    if "%__SavedErrorLevel%"=="0" (
        echo.
        echo Running test !__SourceFileName!
        call !__SourceFile!.cmd
        set __SavedErrorLevel=!ErrorLevel!
    )

:SkipTestRun
    if "%__SavedErrorLevel%"=="0" (
        set /a __PassedTests=%__PassedTests%+1
        echo ^<test name="!__SourceFile!" type="Program" method="Main" result="Pass" /^> >> %__BinDir%\testResults.tmp
    ) ELSE (
        echo ^<test name="!__SourceFile!" type="Program" method="Main" result="Fail"^> >> %__BinDir%\testResults.tmp
        echo ^<failure exception-type="Exit code: %ERRORLEVEL%" ^> >> %__BinDir%\testResults.tmp
        echo     ^<message^>See !__SourceFile!.*.log ^</message^> >> %__BinDir%\testResults.tmp
        echo ^</failure^> >> %__BinDir%\testResults.tmp
        echo ^</test^> >> %__BinDir%\testResults.tmp
    )
    goto :eof

:DeleteFile
    if exist %1 del %1
    goto :eof

:Fail
    echo.
    powershell -Command Write-Host %1 -foreground "red"
    exit /b -1

:TestExtRepo
    echo Running external tests
    if not exist "%CoreRT_TestExtRepo%" ((call :Fail "%CoreRT_TestExtRepo% does not exist") & exit /b -1)
    if not "%CoreRT_BuildExtRepo%" == "false" (
        pushd %CoreRT_TestExtRepo%
        call build.cmd %CoreRT_BuildArch% %CoreRT_BuildType%
        if not !ErrorLevel!==0 ((call :Fail "%CoreRT_TestExtRepo% build failed") & popd & exit /b -1)
        popd
    )

    echo.
    powershell -Command Write-Host "set CLRCustomTestLauncher=%CoreRT_TestRoot%ilc.cmd" -foreground "cyan"
    set CLRCustomTestLauncher=%CoreRT_TestRoot%ilc.cmd

    set CORE_ROOT=%CoreRT_TestExtRepo%\bin\Product\%__BuildStr%
    pushd %CoreRT_TestExtRepo%\tests
    call runtest.cmd %CoreRT_BuildArch% %CoreRT_BuildType% exclude %CoreRT_TestRoot%\CoreCLR.issues.targets
    set __SavedErrorLevel=%ErrorLevel%
    popd
    exit /b %__SavedErrorLevel%
