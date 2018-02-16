@ECHO OFF
SETLOCAL

SET vswherePath=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe
IF NOT EXIST "%vswherePath%" GOTO :ERROR

FOR /F "tokens=*" %%i IN (	'
	  "%vswherePath%" -latest -prerelease -products * ^
        -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 ^
        -property installationPath'
      ) DO SET vsBase=%%i

IF "%vsBase%"=="" GOTO :ERROR

CALL "%vsBase%\vc\Auxiliary\Build\vcvarsall.bat" %1% > NUL

FOR /F "delims=" %%W IN ('where link') DO (
    FOR %%A IN ("%%W") DO ECHO %%~dpA#
    GOTO :CAPTURE_LIB_PATHS
)

:CAPTURE_LIB_PATHS
ECHO %LIB%

EXIT /B 0

ENDLOCAL

:ERROR
    EXIT /B 1
