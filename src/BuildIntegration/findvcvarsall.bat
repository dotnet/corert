@ECHO OFF
SETLOCAL

SET vswherePath=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe
IF NOT EXIST "%vswherePath%" GOTO :ERROR

FOR /F "tokens=*" %%i IN (	'
	  "%vswherePath%" -latest -products * ^
        -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 ^
        -property installationPath'
      ) DO SET vsBase=%%i

IF "%vsBase%"=="" GOTO :ERROR

>"%~dp0\.vcvarsallwithargs" echo("%vsBase%\vc\Auxiliary\Build\vcvarsall.bat" %1%

ENDLOCAL

EXIT /B 0

:ERROR
    ECHO "Visual Studio not found, try to downloading it from https://www.visualstudio.com/ and select Desktop Development for C++ workload."
    EXIT /B 1
