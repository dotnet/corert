@echo off
setlocal EnableDelayedExpansion

REM
REM Script to compile a MSIL assembly to native code. 
REM 
REM Supported code-generators: CPPCODEGEN
REM

if "%VCINSTALLDIR%" == "" (
	echo Please run the script from within VS native tools command prompt
	goto InvalidArgs
)

set __BuildArch=x64
set __Infile=
set __Outfile=
set __LibPath=
set __Temp=%temp%\
set __AppDepSdk=
set __ILToNative=%~dp0
set __CompileMode=cpp
set __LogFilePath=%__Temp%

:Arg_Loop
if "%1" == "" goto ArgsDone
if /i "%1" == "x64"    (set __BuildArch=x64&&shift&goto Arg_Loop)

if /i "%1" == "debug"    (set __BuildType=Debug&shift&goto Arg_Loop)
if /i "%1" == "release"   (set __BuildType=Release&shift&goto Arg_Loop)

if /i "%1" == "/in" (set __Infile=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/out" (set __Outfile=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/appdepsdk" (set __AppDepSdk="%2"&shift&shift&goto Arg_Loop)
if /i "%1" == "/mode" (set __CompileMode="%2"&shift&shift&goto Arg_Loop)
if /i "%1" == "/logpath" (set __LogFilePath="%2"&shift&shift&goto Arg_Loop)

echo Invalid command line argument: %1
goto InvalidArgs

:ArgsDone

REM Do we have valid arguments?
if "%__Infile%" == "" goto InvalidArgs
if "%__Outfile%" == "" goto InvalidArgs
if "%__AppDepSdk%" == "" goto InvalidArgs
if "%__CompileMode%" == "" goto InvalidArgs

REM Set path contain Runtime.lib/PortableRuntime.lib and System.Private.Corelib.dll
set __LibPath="%__ILToNative%\sdk"

REM Validate the code-generation mode
if NOT "%__CompileMode%" == "cpp" goto InvalidArgs

REM *** CPPCodegeneration ***
REM Extract the name of the MSIL file we are compiling
set AssemblyFileName=
set AssemblyExt=
for /f %%i IN ("%__Infile%") DO (
	set AssemblyFileName=%%~ni
	set AssemblyExt=%%~xi
)

set Assembly=%AssemblyFileName%%AssemblyExt%
set CPPFileName="%temp%\%Assembly%.cpp"
set ObjFileName="%temp%\%Assembly%.cpp.obj"

REM Generate the CPP file for the MSIL assembly
echo Generating source file
"%__ILToNative%\ILToNative.exe" %__Infile% -r "%__ILToNative%\sdk\System.Private.CoreLib.dll" -r "%__AppDepSdk%\*.dll" -out "%CPPFileName%" -cpp > %__LogFilePath%\ILToNative.MSILToCpp.log
if ERRORLEVEL 1 (
	echo Unable to generate CPP file.
	goto :eof
)

set DefinesDebug=/ZI /nologo /W3 /WX- /sdl /Od /D CPPCODEGEN /D WIN32 /D _DEBUG /D _CONSOLE /D _LIB /D _UNICODE /D UNICODE /Gm /EHsc /RTC1 /MDd /GS /fp:precise /Zc:wchar_t /Zc:forScope /Zc:inline

set DefinesRelease=/Zi /nologo /W3 /WX- /sdl /O2 /Oi /GL /D CPPCODEGEN /D WIN32 /D NDEBUG /D _CONSOLE /D _LIB /D _UNICODE /D UNICODE /Gm- /EHsc /MD /GS /Gy /fp:precise /Zc:wchar_t /Zc:forScope /Zc:inline

set LinkDebug=
set LinkRelease=/INCREMENTAL:NO /OPT:REF /OPT:ICF /LTCG:incremental


set CPPDefines=%DefinesDebug%
set LinkOpts=%LinkDebug%
if "%__BuildType%" == "Release" (
	set CPPDefines=%DefinesRelease%
	set LinkOpts=%LinkRelease%
)

REM Now compile the CPP file to platform specific executable.

echo Compiling application source files
"%VCINSTALLDIR%\bin\x86_amd64\CL.exe" /c /I %__AppDepSdk%\CPPSdk\Windows_NT /I %__AppDepSdk%\CPPSdk\ %CPPDefines% /Fo"%ObjFileName%" /Gd /TP /wd4477 /errorReport:prompt %CPPFileName% > %__LogFilePath%\ILToNative.App.log
if ERRORLEVEL 1 (
	echo Unable to compile app source file.
	goto :eof
)

echo Generating native executable
"%VCINSTALLDIR%\bin\x86_amd64\link.exe" /ERRORREPORT:PROMPT /OUT:"%__Outfile%" /NOLOGO kernel32.lib user32.lib gdi32.lib winspool.lib comdlg32.lib advapi32.lib shell32.lib ole32.lib oleaut32.lib uuid.lib odbc32.lib odbccp32.lib kernel32.lib user32.lib gdi32.lib winspool.lib comdlg32.lib advapi32.lib shell32.lib ole32.lib oleaut32.lib uuid.lib odbc32.lib odbccp32.lib %__LibPath%\PortableRuntime.lib %__LibPath%\bootstrappercpp.lib /MANIFEST /MANIFESTUAC:"level='asInvoker' uiAccess='false'" /manifest:embed /Debug /SUBSYSTEM:CONSOLE /TLBID:1 /DYNAMICBASE /NXCOMPAT %LinkOpts% /MACHINE:%__BuildArch% "%ObjFileName%" > %__LogFilePath%\ILToNative.Link.log
if ERRORLEVEL 1 (
	echo Unable to link native executable.
	goto :eof
)

:BuildComplete
echo Build successfully completed.
goto :eof

:InvalidArgs
echo
echo Usage: dotnet-compile-native <arch> <buildType> /in <path to MSIL assembly> /out <path to native executable> /appdepsdk <path to contents of Microsoft.DotNet.AppDep nuget package> [/mode cpp] [/logpath <path to drop logfiles in>]
goto :eof

