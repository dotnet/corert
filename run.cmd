@if not defined _echo @echo off
setlocal

if not defined VisualStudioVersion (
  if defined VS140COMNTOOLS (
    call "%VS140COMNTOOLS%\VsDevCmd.bat"
    goto :Run
  )
  echo Error: Visual Studio 2015 required.
  echo        Please see https://github.com/dotnet/corefx/blob/master/Documentation/project-docs/developer-guide.md for build instructions.
  exit /b 1
)

:Run
:: Clear the 'Platform' env variable for this session, as it's a per-project setting within the build, and
:: misleading value (such as 'MCD' in HP PCs) may lead to build breakage (issue: #69).
set Platform=
set TOOLRUNTIME_DIR=%~dp0Tools
set BOOTSTRAP_URL=https://raw.githubusercontent.com/dotnet/buildtools/master/bootstrap/bootstrap.ps1
set BOOTSTRAP_DEST=%TOOLRUNTIME_DIR%\bootstrap.ps1
set /p DOTNET_VERSION=< "%~dp0.cliversion"
set SHARED_FRAMEWORK_VERSION=1.0.0-rc3-002733


REM Disable first run since we want to control all package sources
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
if not exist %TOOLRUNTIME_DIR% (
  mkdir %TOOLRUNTIME_DIR%
)
if not exist %BOOTSTRAP_DEST% (
  powershell -NoProfile -ExecutionPolicy unrestricted -Command "$retryCount = 0; $success = $false; do { try { (New-Object Net.WebClient).DownloadFile('%BOOTSTRAP_URL%', '%BOOTSTRAP_DEST%'); $success = $true; } catch { if ($retryCount -ge 6) { throw; } else { $retryCount++; Start-Sleep -Seconds (5 * $retryCount); } } } while ($success -eq $false)"
)
powershell -NoProfile -ExecutionPolicy unrestricted %BOOTSTRAP_DEST% -RepositoryRoot (Get-Location) 

if NOT [%ERRORLEVEL%]==[0] exit /b 1


set _dotnet=%TOOLRUNTIME_DIR%\dotnetcli\dotnet.exe

call %_dotnet% %TOOLRUNTIME_DIR%\run.exe %*
exit /b %ERRORLEVEL%
