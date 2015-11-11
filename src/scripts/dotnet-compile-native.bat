@echo off
setlocal EnableDelayedExpansion

REM
REM Script to compile a MSIL assembly to native code. 
REM 
REM Supported code-generators: CPPCODEGEN, ProtoJIT
REM

if "%VS140COMNTOOLS%" == "" (
	echo Please install Microsoft Visual Studio 2015.
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
set __CodegenPath=
set __ObjgenPath=
set __LinkLibs=

:Arg_Loop
if "%1" == "" goto :ArgsDone
if /i "%1" == "x64"    (set __BuildArch=x64&&shift&goto Arg_Loop)

if /i "%1" == "debug"    (set __BuildType=Debug&shift&goto Arg_Loop)
if /i "%1" == "release"   (set __BuildType=Release&shift&goto Arg_Loop)

if /i "%1" == "/in" (set __Infile=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/out" (set __Outfile=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/appdepsdk" (set __AppDepSdk=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/mode" (set __CompileMode=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/logpath" (set __LogFilePath=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/codegenpath" (set __CodegenPath=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/objgenpath" (set __ObjgenPath=%2&shift&shift&goto Arg_Loop)
if /i "%1" == "/linklibs" (set __LinkLibs=%2&shift&shift&goto Arg_Loop)

echo Invalid command line argument: %1
goto :InvalidArgs

:ArgsDone

REM Do we have valid arguments?
if "%__Infile%" == "" (
	echo Please specify MSIL assembly to be compiled.
	goto :InvalidArgs
)	
if "%__Outfile%" == "" (
	echo Please specify the native executable to be generated.
	goto :InvalidArgs
)

if "%__AppDepSdk%" == "" (
	echo Please specify the path to the extracted Microsoft.DotNet.AppDep nuget package.
	goto :InvalidArgs
)

if "%__CompileMode%" == "" (
	echo Please specify a valid compilation mode.
	goto :InvalidArgs
)

REM Set path contain Runtime.lib/PortableRuntime.lib and System.Private.Corelib.dll
set __LibPath=%__ILToNative%\sdk

REM Initialize environment to invoke native tools
call "%VS140COMNTOOLS%\..\..\VC\vcvarsall.bat" x86_amd64

REM Extract the name of the MSIL file we are compiling
set AssemblyFileName=
set AssemblyExt=
for /f %%i IN (%__Infile%) DO (
	set AssemblyFileName=%%~ni
	set AssemblyExt=%%~xi
)

set Assembly=%AssemblyFileName%%AssemblyExt%

REM Validate the code-generation mode
if /i "%__CompileMode%" == "cpp" goto :ModeCPP
if /i "%__CompileMode%" == "protojit" goto :ModeProtoJIT
echo Please specify a valid compilation mode.
goto :InvalidArgs

:ModeProtoJIT
REM *** ProtoJIT Codegen ***
REM Generate the obj file for the MSIL assembly

set ObjFileName=%__Infile%.obj
call :DeleteFile %ObjFileName%
set libBootstrapper=%__LibPath%\bootstrapper.lib
set libRuntime=%__LibPath%\Runtime.lib

if not exist "%__CodegenPath%\%__CompileMode%.dll" (
	echo Unable to find %__CodegenPath%\%__CompileMode%.dll to compile application!
	goto :InvalidArgs
)

if not exist "%__ObjgenPath%\objwriter.dll" (
	echo Unable to find %__ObjgenPath%\objwriter.dll to generate generate binaries for application!
	goto :InvalidArgs
)

setlocal
REM Setup the path to include the location of the codegenerator and binary file generator
REM so that they can be located by the OS loader.
set path=%__CodegenPath%;%__ObjgenPath%;%path%
echo Generating app obj file
"%__ILToNative%\ILToNative.exe" %__Infile% -r "%__ILToNative%\sdk\System.Private.CoreLib.dll" -r %__AppDepSdk%\*.dll -out %ObjFileName% > %__LogFilePath%\ILToNative.App.log
endlocal

set EXITCode=%ERRORLEVEL%
if %EXITCode% GEQ 1 (
	echo Unable to generate App object file.
	goto :FailedExit
)

REM We successfully generated the object file, so proceed to link phase
goto :LinkObj

:ModeCPP
REM *** CPPCodegeneration ***
set CPPFileName=%__Infile%.cpp
set ObjFileName=%__Infile%.cpp.obj
set libBootstrapper=%__LibPath%\bootstrappercpp.lib
set libRuntime=%__LibPath%\PortableRuntime.lib

REM Perform basic cleanup
call :DeleteFile "%CPPFileName%"
call :DeleteFile "%ObjFileName%"
call :DeleteFile "%__Outfile%"


REM Generate the CPP file for the MSIL assembly
echo Generating source file
"%__ILToNative%\ILToNative.exe" %__Infile% -r "%__ILToNative%\sdk\System.Private.CoreLib.dll" -r %__AppDepSdk%\*.dll -out "%CPPFileName%" -cpp > %__LogFilePath%\ILToNative.MSILToCpp.log
if ERRORLEVEL 1 (
	echo Unable to generate CPP file.
	goto :FailedExit
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
	goto :FailedExit
)

:LinkObj
echo Generating native executable
"%VCINSTALLDIR%\bin\x86_amd64\link.exe" /ERRORREPORT:PROMPT /OUT:"%__Outfile%" /NOLOGO kernel32.lib user32.lib gdi32.lib winspool.lib comdlg32.lib advapi32.lib shell32.lib ole32.lib oleaut32.lib uuid.lib odbc32.lib odbccp32.lib %libRuntime% %libBootstrapper% %__LinkLibs% /MANIFEST /MANIFESTUAC:"level='asInvoker' uiAccess='false'" /manifest:embed /Debug /SUBSYSTEM:CONSOLE /TLBID:1 /DYNAMICBASE /NXCOMPAT %LinkOpts% /MACHINE:%__BuildArch% "%ObjFileName%" > %__LogFilePath%\ILToNative.Link.log
if ERRORLEVEL 1 (
	echo Unable to link native executable.
	goto :FailedExit
)

:BuildComplete
echo Build successfully completed.
exit /B 0

:DeleteFile
if exist %1 del %1
goto :eof

:InvalidArgs
echo Invalid command line
echo.
echo Usage: dotnet-compile-native arch buildType /in path-to-MSIL-assembly /out path-to-native executable /appdepsdk path to contents of Microsoft.DotNet.AppDep nuget package [/mode cpp|protojit /codegenpath path to contents of Microsoft.DotNet.ProtoJit package /objgenpath path to contents of Microsoft.DotNet.ObjWriter package] [/logpath path to drop logfiles in]

:FailedExit
exit /B 1

