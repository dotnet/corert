@if not defined _echo @echo off
setlocal EnableDelayedExpansion

set ThisScript=%0
set CoreRT_TestRoot=%~dp0
set CoreRT_CliDir=%CoreRT_TestRoot%../Tools/dotnetcli
set CoreRT_BuildArch=x64
set CoreRT_BuildType=Debug
set CoreRT_BuildOS=Windows_NT
set CoreRT_TestRun=true
set CoreRT_TestCompileMode=
set CoreRT_RunCoreCLRTests=
set CoreRT_CoreCLRTargetsFile=
set CoreRT_TestLogFileName=testresults.xml
set CoreRT_TestName=*

:ArgLoop
if "%1" == "" goto :ArgsDone
if /i "%1" == "/?" goto :Usage
if /i "%1" == "x64"    (set CoreRT_BuildArch=x64&&shift&goto ArgLoop)
if /i "%1" == "x86"    (set CoreRT_BuildArch=x86&&shift&goto ArgLoop)
if /i "%1" == "arm"    (set CoreRT_BuildArch=arm&&shift&goto ArgLoop)

if /i "%1" == "debug"    (set CoreRT_BuildType=Debug&shift&goto ArgLoop)
if /i "%1" == "release"  (set CoreRT_BuildType=Release&shift&goto ArgLoop)
if /i "%1" == "/coreclr"  (
    set CoreRT_RunCoreCLRTests=true
    set SelectedTests=%2&shift&shift
    if "!SelectedTests!" == "" (
        set CoreRT_CoreCLRTargetsFile=%CoreRT_TestRoot%\Top200.CoreCLR.issues.targets
        goto :ExtRepoTestsOk
    )

    if /i "!SelectedTests!" == "All" (
        set CoreRT_CoreCLRTargetsFile=
        set CoreCLRExcludeText=
        goto :ExtRepoTestsOk
    )

    if /i "!SelectedTests!" == "Top200" set CoreRT_CoreCLRTargetsFile=%CoreRT_TestRoot%\Top200.CoreCLR.issues.targets&&goto :ExtRepoTestsOk
    if /i "!SelectedTests!" == "KnownGood" set CoreRT_CoreCLRTargetsFile=%CoreRT_TestRoot%\CoreCLR.issues.targets&&goto :ExtRepoTestsOk
    if /i "!SelectedTests!" == "Interop" set CoreRT_CoreCLRTargetsFile=%CoreRT_TestRoot%\Interop.CoreCLR.issues.targets&&goto :ExtRepoTestsOk

    echo Invalid test selection specified: !SelectedTests!
    goto :Usage

:ExtRepoTestsOk
    goto ArgLoop
)
if /i "%1" == "/coreclrsingletest" (set CoreRT_RunCoreCLRTests=true&set CoreRT_CoreCLRTest=%2&shift&shift&goto ArgLoop)
if /i "%1" == "/mode" (set CoreRT_TestCompileMode=%2&shift&shift&goto ArgLoop)
if /i "%1" == "/test" (set CoreRT_TestName=%2&shift&shift&goto ArgLoop)
if /i "%1" == "/runtest" (set CoreRT_TestRun=%2&shift&shift&goto ArgLoop)
if /i "%1" == "/dotnetclipath" (set CoreRT_CliDir=%2&shift&shift&goto ArgLoop)
if /i "%1" == "/multimodule" (set CoreRT_MultiFileConfiguration=MultiModule&shift&goto ArgLoop)

echo Invalid command line argument: %1
goto :Usage

:Usage
echo %ThisScript% [arch] [flavor] [/mode] [/runtest] [/coreclr ^<subset^>]
echo     arch          : x64 / x86 / arm
echo     flavor        : debug / release
echo     /mode         : Optionally restrict to a single code generator. Specify cpp/ryujit/wasm. Default: all
echo     /test         : Run a single test by folder name (ie, BasicThreading)
echo     /runtest      : Should just compile or run compiled binary? Specify: true/false. Default: true.
echo     /coreclr      : Download and run the CoreCLR repo tests
echo     /coreclrsingletest ^<absolute\path\to\test.exe^>
echo                   : Run a single CoreCLR repo test
echo     /multimodule  : Compile the framework as a .lib and link tests against it (only supports ryujit)
echo.
echo     --- CoreCLR Subset ---
echo        Top200     : Runs broad coverage / CI validation (~200 tests).
echo        KnownGood  : Runs tests known to pass on CoreRT (~6000 tests).
echo        Interop    : Runs only the interop tests (~43 tests).
echo        All        : Runs all tests. There will be many failures (~7000 tests).
exit /b 2

:ArgsDone

if /i "%CoreRT_TestCompileMode%"=="jit" (
    set CoreRT_TestCompileMode=ryujit
)

:: Cpp Codegen does not support multi-module compilation, so force Ryujit
if "%CoreRT_MultiFileConfiguration%"=="MultiModule" (
    set CoreRT_TestCompileMode=ryujit
)

call %CoreRT_TestRoot%testenv.cmd

set CoreRT_RspTemplateDir=%CoreRT_TestRoot%..\bin\obj\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%

set __BuildStr=%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%
set __CoreRTTestBinDir=%CoreRT_TestRoot%..\bin\tests

:: Place test logs in a subfolder in multi-module mode so both single-file and
:: multi-module test results are visible to the CI tooling
if NOT "%CoreRT_MultiFileConfiguration%" == "" (
    set CoreRT_TestLogFileName=%CoreRT_MultiFileConfiguration%\%CoreRT_TestLogFileName%
    if not exist %__CoreRTTestBinDir%\%CoreRT_MultiFileConfiguration%\ mkdir %__CoreRTTestBinDir%\%CoreRT_MultiFileConfiguration%
)

set __LogDir=%CoreRT_TestRoot%\..\bin\Logs\%__BuildStr%\tests

if defined VisualStudioVersion goto :RunVCVars

set _VSWHERE="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist %_VSWHERE% (
  for /f "usebackq tokens=*" %%i in (`%_VSWHERE% -latest -prerelease -property installationPath`) do set _VSCOMNTOOLS=%%i\Common7\Tools
)

call "%_VSCOMNTOOLS%\VsDevCmd.bat"

:RunVCVars

call "!VS150COMNTOOLS!\..\..\VC\Auxiliary\Build\vcvarsall.bat" %CoreRT_BuildArch%

if "%CoreRT_RunCoreCLRTests%"=="true" goto :TestExtRepo

if /i "%__BuildType%"=="Debug" (
    set __LinkLibs=msvcrtd.lib
) else (
    set __LinkLibs=msvcrt.lib
)

if not exist "!__CoreRTTestBinDir!" (mkdir !__CoreRTTestBinDir!)
echo. > %__CoreRTTestBinDir%\testResults.tmp

set /a __CppTotalTests=0
set /a __CppPassedTests=0
set /a __JitTotalTests=0
set /a __JitPassedTests=0
set /a __WasmTotalTests=0
set /a __WasmPassedTests=0
for /f "delims=" %%a in ('dir /s /aD /b %CoreRT_TestRoot%\src\%CoreRT_TestName%') do (
    set __SourceFolder=%%a
    set __SourceFileName=%%~na
    set __RelativePath=!__SourceFolder:%CoreRT_TestRoot%=!
    set __SourceFileProj=
    if exist "!__SourceFolder!\!__SourceFileName!.csproj" (
        set __SourceFileProj=!__SourceFolder!\!__SourceFileName!.csproj
    )
    if exist "!__SourceFolder!\!__SourceFileName!.ilproj" (
        set __SourceFileProj=!__SourceFolder!\!__SourceFileName!.ilproj
    )
    if NOT "!__SourceFileProj!" == "" (
        if /i not "%CoreRT_TestCompileMode%" == "cpp" (
            if /i not "%CoreRT_TestCompileMode%" == "wasm" (
                    if not exist "!__SourceFolder!\no_ryujit" (
                    set __Mode=Jit
                    call :CompileFile !__SourceFolder! !__SourceFileName! !__SourceFileProj! %__LogDir%\!__RelativePath!
                    set /a __JitTotalTests=!__JitTotalTests!+1
                )
            )
        )
        if /i not "%CoreRT_TestCompileMode%" == "ryujit" (
            if /i not "%CoreRT_TestCompileMode%" == "wasm" (
                if not exist "!__SourceFolder!\no_cpp" (
                    set __Mode=Cpp
                    call :CompileFile !__SourceFolder! !__SourceFileName! !__SourceFileProj! %__LogDir%\!__RelativePath!
                    set /a __CppTotalTests=!__CppTotalTests!+1
                )
            )
            if /i not "%CoreRT_TestCompileMode%" == "cpp" (
                if exist "!__SourceFolder!\wasm" (
                    set __Mode=wasm
                    call :CompileFile !__SourceFolder! !__SourceFileName! !__SourceFileProj! %__LogDir%\!__RelativePath!
                    set /a __WasmTotalTests=!__WasmTotalTests!+1
                )
            )
        )
    )
)
set /a __CppFailedTests=%__CppTotalTests%-%__CppPassedTests%
set /a __JitFailedTests=%__JitTotalTests%-%__JitPassedTests%
set /a __WasmFailedTests=%__WasmTotalTests%-%__WasmPassedTests%
set /a __TotalTests=%__JitTotalTests%+%__CppTotalTests%+%__WasmTotalTests%
set /a __PassedTests=%__JitPassedTests%+%__CppPassedTests%+%__WasmPassedTests%
set /a __FailedTests=%__JitFailedTests%+%__CppFailedTests%+%__WasmFailedTests%

echo ^<assemblies^>  > %__CoreRTTestBinDir%\%CoreRT_TestLogFileName%
echo ^<assembly name="ILCompiler" total="%__TotalTests%" passed="%__PassedTests%" failed="%__FailedTests%" skipped="0"^>  >> %__CoreRTTestBinDir%\%CoreRT_TestLogFileName%
echo ^<collection total="%__TotalTests%" passed="%__PassedTests%" failed="%__FailedTests%" skipped="0"^>  >> %__CoreRTTestBinDir%\%CoreRT_TestLogFileName%
type %__CoreRTTestBinDir%\testResults.tmp >> %__CoreRTTestBinDir%\%CoreRT_TestLogFileName%
echo ^</collection^>  >> %__CoreRTTestBinDir%\%CoreRT_TestLogFileName%
echo ^</assembly^>  >> %__CoreRTTestBinDir%\%CoreRT_TestLogFileName%
echo ^</assemblies^>  >> %__CoreRTTestBinDir%\%CoreRT_TestLogFileName%

echo.
set __JitStatusPassed=1
set __CppStatusPassed=1
set __WasmStatusPassed=1

if /i not "%CoreRT_TestCompileMode%" == "cpp" (
    set __JitStatusPassed=0
    if %__JitTotalTests% EQU %__JitPassedTests% (set __JitStatusPassed=1)
    if %__JitTotalTests% EQU 0 (set __JitStatusPassed=1)
    call :PassFail !__JitStatusPassed! "JIT - TOTAL: %__JitTotalTests% PASSED: %__JitPassedTests%"
)

if /i not "%CoreRT_TestCompileMode%" == "ryujit" (
    set __CppStatusPassed=0
    if %__CppTotalTests% EQU %__CppPassedTests% (set __CppStatusPassed=1)
    if %__CppTotalTests% EQU 0 (set __CppStatusPassed=1)
    call :PassFail !__CppStatusPassed! "CPP - TOTAL: %__CppTotalTests% PASSED: %__CppPassedTests%"
)


if /i not "%CoreRT_TestCompileMode%" == "ryujit" (
    set __WasmStatusPassed=0
    if %__WasmTotalTests% EQU %__WasmPassedTests% (set __WasmStatusPassed=1)
    if %__WasmTotalTests% EQU 0 (set __WasmStatusPassed=1)
    call :PassFail !__WasmStatusPassed! "WASM - TOTAL: %__WasmTotalTests% PASSED: %__WasmPassedTests%"
)

if not !__JitStatusPassed! EQU 1 (exit /b 1)
if not !__CppStatusPassed! EQU 1 (exit /b 1)
if not !__WasmStatusPassed! EQU 1 (exit /b 1)
exit /b 0

:PassFail
set __Green=%~1
set __OutStr=%~2
if "%__Green%"=="1" (
    powershell -Command Write-Host %__OutStr% -foreground "Black" -background "Green"
) else ( 
    powershell -Command Write-Host %__OutStr% -foreground "White" -background "Red"
)
goto :eof

:CompileFile
    echo.
    set __SourceFolder=%~1
    set __SourceFileName=%~2
    set __SourceFileProj=%~3
    set __CompileLogPath=%~4

    echo Compiling directory !__SourceFolder! !__Mode!
    echo.

    if not exist "!__CompileLogPath!" (mkdir !__CompileLogPath!)
    set __SourceFile=!__SourceFolder!\!__SourceFileName!

    if exist "!__SourceFolder!\bin" rmdir /s /q !__SourceFolder!\bin
    if exist "!__SourceFolder!\obj" rmdir /s /q !__SourceFolder!\obj

    setlocal
    set extraArgs=
    if /i "%__Mode%" == "cpp" (
        set extraArgs=!extraArgs! /p:NativeCodeGen=cpp
        if /i "%CoreRT_BuildType%" == "debug" (
            set extraArgs=!extraArgs! /p:UseDebugCrt=true
        )
    ) else if /i "%__Mode%" == "wasm" (
        set extraArgs=!extraArgs! /p:NativeCodeGen=wasm
    ) else (
        if "%CoreRT_MultiFileConfiguration%" == "MultiModule" (
            set extraArgs=!extraArgs! "/p:IlcMultiModule=true"
        )
    )

    echo msbuild /m /ConsoleLoggerParameters:ForceNoAlign "/p:IlcPath=%CoreRT_ToolchainDir%" "/p:Configuration=%CoreRT_BuildType%" "/p:Platform=%CoreRT_BuildArch%" "/p:RepoLocalBuild=true" "/p:FrameworkLibPath=%~dp0..\bin\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\lib" "/p:FrameworkObjPath=%~dp0..\bin\obj\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\Framework" !extraArgs! !__SourceFileProj!
    echo.
    msbuild /m /ConsoleLoggerParameters:ForceNoAlign "/p:IlcPath=%CoreRT_ToolchainDir%" "/p:Configuration=%CoreRT_BuildType%" "/p:Platform=%CoreRT_BuildArch%" "/p:RepoLocalBuild=true" "/p:FrameworkLibPath=%~dp0..\bin\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\lib" "/p:FrameworkObjPath=%~dp0..\bin\obj\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\Framework" !extraArgs! !__SourceFileProj!
    endlocal

    set __SavedErrorLevel=%ErrorLevel%
    if "%CoreRT_TestRun%"=="false" (goto :SkipTestRun)
    if "%__Mode%" == "wasm" (goto :SkipTestRun)

    if "%__SavedErrorLevel%"=="0" (
        echo.
        echo Running test !__SourceFileName!
        call !__SourceFile!.cmd !__SourceFolder!\bin\%CoreRT_BuildType%\%CoreRT_BuildArch%\native !__SourceFileName!.exe
        set __SavedErrorLevel=!ErrorLevel!
    )

:SkipTestRun
    if "%__SavedErrorLevel%"=="0" (
        set /a __%__Mode%PassedTests=!__%__Mode%PassedTests!+1
        echo ^<test name="!__SourceFile!" type="!__SourceFileName!:%__Mode%" method="Main" result="Pass" /^> >> %__CoreRTTestBinDir%\testResults.tmp
    ) ELSE (
        echo ^<test name="!__SourceFile!" type="!__SourceFileName!:%__Mode%" method="Main" result="Fail"^> >> %__CoreRTTestBinDir%\testResults.tmp
        echo ^<failure exception-type="Exit code: %ERRORLEVEL%" ^> >> %__CoreRTTestBinDir%\testResults.tmp
        echo     ^<message^>See !__SourceFile! \bin or \obj for logs ^</message^> >> %__CoreRTTestBinDir%\testResults.tmp
        echo ^</failure^> >> %__CoreRTTestBinDir%\testResults.tmp
        echo ^</test^> >> %__CoreRTTestBinDir%\testResults.tmp
    )
    goto :eof

:DeleteFile
    if exist %1 del %1
    goto :eof

:Fail
    echo.
    powershell -Command Write-Host %1 -foreground "red"
    exit /b -1

:RestoreCoreCLRTests

    set TESTS_SEMAPHORE=%CoreRT_TestExtRepo%\init-tests.completed

    :: If sempahore exists do nothing
    if exist "%TESTS_SEMAPHORE%" (
      echo Tests are already initialized.
      goto :EOF
    )

    if exist "%CoreRT_TestExtRepo%" rmdir /S /Q "%CoreRT_TestExtRepo%"
    mkdir "%CoreRT_TestExtRepo%"

    set /p TESTS_REMOTE_URL=< "%~dp0\CoreCLRTestsURL.txt"
    set TESTS_LOCAL_ZIP=%CoreRT_TestExtRepo%\tests.zip
    set INIT_TESTS_LOG=%~dp0..\init-tests.log
    echo Restoring tests (this may take a few minutes)..
    echo Installing '%TESTS_REMOTE_URL%' to '%TESTS_LOCAL_ZIP%' >> "%INIT_TESTS_LOG%"
    powershell -NoProfile -ExecutionPolicy unrestricted -Command "$retryCount = 0; $success = $false; do { try { [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12; (New-Object Net.WebClient).DownloadFile('%TESTS_REMOTE_URL%', '%TESTS_LOCAL_ZIP%'); $success = $true; } catch { if ($retryCount -ge 6) { throw; } else { $retryCount++; Start-Sleep -Seconds (5 * $retryCount); } } } while ($success -eq $false); Add-Type -Assembly 'System.IO.Compression.FileSystem' -ErrorVariable AddTypeErrors; if ($AddTypeErrors.Count -eq 0) { [System.IO.Compression.ZipFile]::ExtractToDirectory('%TESTS_LOCAL_ZIP%', '%CoreRT_TestExtRepo%') } else { (New-Object -com shell.application).namespace('%CoreRT_TestExtRepo%').CopyHere((new-object -com shell.application).namespace('%TESTS_LOCAL_ZIP%').Items(),16) }" >> "%INIT_TESTS_LOG%"
    if errorlevel 1 (
      echo ERROR: Could not download CoreCLR tests correctly. See '%INIT_TESTS_LOG%' for more details. 1>&2
      exit /b 1
    )

    echo Tests restored.
    echo CoreCLR tests restored from %TESTS_REMOTE_URL% > %TESTS_SEMAPHORE%
    exit /b 0

:TestExtRepo
    :: Omit the exclude parameter to CoreCLR's test harness if we're running all tests
    set CoreCLRExcludeText=exclude
    if "%CoreRT_CoreCLRTargetsFile%" == "" (
        set CoreCLRExcludeText=
    )

    echo Running external tests
    if "%CoreRT_TestExtRepo%" == "" (
        set CoreRT_TestExtRepo=%CoreRT_TestRoot%\..\tests_downloaded\CoreCLR
        call :RestoreCoreCLRTests
        if errorlevel 1 (
            exit /b 1
        )
    )

    if not exist "%CoreRT_TestExtRepo%" ((call :Fail "%CoreRT_TestExtRepo% does not exist") & exit /b 1)

    if "%CoreRT_MultiFileConfiguration%" == "MultiModule" (
        set IlcMultiModule=true
        REM Pre-compile shared framework assembly
        echo Compiling framework library
        echo msbuild /m /ConsoleLoggerParameters:ForceNoAlign "/p:IlcPath=%CoreRT_ToolchainDir%" "/p:Configuration=%CoreRT_BuildType%" "/p:Platform=%CoreRT_BuildArch%" "/p:RepoLocalBuild=true" "/p:FrameworkLibPath=%CoreRT_TestRoot%..\bin\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\lib" "/p:FrameworkObjPath=%~dp0..\bin\obj\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\Framework" /t:CreateLib %CoreRT_TestRoot%\..\src\BuildIntegration\BuildFrameworkNativeObjects.proj
        msbuild /m /ConsoleLoggerParameters:ForceNoAlign "/p:IlcPath=%CoreRT_ToolchainDir%" "/p:Configuration=%CoreRT_BuildType%" "/p:Platform=%CoreRT_BuildArch%" "/p:RepoLocalBuild=true" "/p:FrameworkLibPath=%CoreRT_TestRoot%..\bin\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\lib" "/p:FrameworkObjPath=%~dp0..\bin\obj\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\Framework" /t:CreateLib %CoreRT_TestRoot%\..\src\BuildIntegration\BuildFrameworkNativeObjects.proj
    )

    echo.
    set CLRCustomTestLauncher=%CoreRT_TestRoot%\CoreCLR\build-and-run-test.cmd
    set XunitTestBinBase=!CoreRT_TestExtRepo!
    pushd %CoreRT_TestRoot%\CoreCLR\runtest

    "%CoreRT_CliDir%\dotnet.exe" msbuild /t:Restore /p:RepoLocalBuild=true src\TestWrappersConfig\XUnitTooling.depproj
    if errorlevel 1 (
        exit /b 1
    )

    if not "%CoreRT_CoreCLRTest%" == "" (
        if not exist "%CoreRT_CoreCLRTest%" (
            echo Target test file not found: %CoreRT_CoreCLRTest%
            exit /b 1
        )

        for %%i in (%CoreRT_CoreCLRTest%) do (
            set TestFolderName=%%~dpi
            set TestFileName=%%~nxi
        )
        call %CoreRT_TestRoot%\CoreCLR\build-and-run-test.cmd !TestFolderName! !TestFileName!
    ) else (
        echo runtest.cmd %CoreRT_BuildArch% %CoreRT_BuildType% %CoreCLRExcludeText% %CoreRT_CoreCLRTargetsFile% LogsDir %__LogDir%
        call runtest.cmd %CoreRT_BuildArch% %CoreRT_BuildType% %CoreCLRExcludeText% %CoreRT_CoreCLRTargetsFile% LogsDir %__LogDir%
    )
    
    set __SavedErrorLevel=%ErrorLevel%
    popd
    exit /b %__SavedErrorLevel%
