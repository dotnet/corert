@if not defined _echo @echo off
setlocal

set INIT_TOOLS_LOG=%~dp0init-tools.log
if [%PACKAGES_DIR%]==[] set PACKAGES_DIR=%~dp0packages
if [%TOOLRUNTIME_DIR%]==[] set TOOLRUNTIME_DIR=%~dp0Tools
set DOTNET_PATH=%TOOLRUNTIME_DIR%\dotnetcli\
if [%DOTNET_CMD%]==[] set DOTNET_CMD=%DOTNET_PATH%dotnet.exe
if [%BUILDTOOLS_SOURCE%]==[] set BUILDTOOLS_SOURCE=https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json
set /P BUILDTOOLS_VERSION=< "%~dp0BuildToolsVersion.txt"
set BUILD_TOOLS_PATH=%PACKAGES_DIR%\Microsoft.DotNet.BuildTools\%BUILDTOOLS_VERSION%\lib
set INIT_TOOLS_RESTORE_PROJECT=%~dp0init-tools.msbuild
set BUILD_TOOLS_SEMAPHORE_DIR=%TOOLRUNTIME_DIR%\%BUILDTOOLS_VERSION%
set BUILD_TOOLS_SEMAPHORE=%BUILD_TOOLS_SEMAPHORE_DIR%\init-tools.completed

:: if force option is specified then clean the tool runtime and build tools package directory to force it to get recreated
if [%1]==[force] (
  if exist "%TOOLRUNTIME_DIR%" rmdir /S /Q "%TOOLRUNTIME_DIR%"
  if exist "%PACKAGES_DIR%\Microsoft.DotNet.BuildTools" rmdir /S /Q "%PACKAGES_DIR%\Microsoft.DotNet.BuildTools"
)

if not exist "%BUILD_TOOLS_SEMAPHORE%" (
  goto :restorebuildtools
)

::
:: The init tools semaphore stores the most-recent git commit hash of this file.
:: When changes to this file are synced, init-tools will refresh the existing tools.
::
git rev-list -1 HEAD init-tools.cmd > %BUILD_TOOLS_SEMAPHORE_DIR%\current
if ERRORLEVEL 1 (
  echo ERROR: Could not determine most recent commit hash for init-tools.cmd
  exit /b 1
)

set /p INIT_TOOLS_CURRENT_HASH=<"%BUILD_TOOLS_SEMAPHORE_DIR%\current"
echo Current build tools script hash is %INIT_TOOLS_CURRENT_HASH%
set /p INIT_TOOLS_LAST_RESTORE_HASH=<"%BUILD_TOOLS_SEMAPHORE%"
echo Last build tools restore hash is %INIT_TOOLS_LAST_RESTORE_HASH%

:: Skip restore if init-tools.cmd's most recent commit hash matches the hash
:: in the semaphore file.
if "%INIT_TOOLS_CURRENT_HASH%"=="%INIT_TOOLS_LAST_RESTORE_HASH%" (
  echo Tools are up to date
  goto :EOF
)

:restorebuildtools
if exist "%TOOLRUNTIME_DIR%" rmdir /S /Q "%TOOLRUNTIME_DIR%"

if exist "%DotNetBuildToolsDir%" (
  echo Using tools from '%DotNetBuildToolsDir%'.
  mklink /j "%TOOLRUNTIME_DIR%" "%DotNetBuildToolsDir%"

  if not exist "%DOTNET_CMD%" (
    echo ERROR: Ensure that '%DotNetBuildToolsDir%' contains the .NET Core SDK at '%DOTNET_PATH%'
    exit /b 1
  )

  echo Done initializing tools.
  if NOT exist "%BUILD_TOOLS_SEMAPHORE_DIR%" mkdir "%BUILD_TOOLS_SEMAPHORE_DIR%"
  echo %INIT_TOOLS_CURRENT_HASH%> "%BUILD_TOOLS_SEMAPHORE%"
  exit /b 0
)

echo Running %0 > "%INIT_TOOLS_LOG%"

set /p DOTNET_VERSION=< "%~dp0DotnetCLIVersion.txt"
if exist "%DOTNET_CMD%" goto :afterdotnetrestore

echo Installing dotnet cli...
if NOT exist "%DOTNET_PATH%" mkdir "%DOTNET_PATH%"
set DOTNET_ZIP_NAME=dotnet-sdk-%DOTNET_VERSION%-win-x64.zip
set DOTNET_REMOTE_PATH=https://dotnetcli.azureedge.net/dotnet/Sdk/%DOTNET_VERSION%/%DOTNET_ZIP_NAME%
set DOTNET_LOCAL_PATH=%DOTNET_PATH%%DOTNET_ZIP_NAME%
echo Installing '%DOTNET_REMOTE_PATH%' to '%DOTNET_LOCAL_PATH%' >> "%INIT_TOOLS_LOG%"
powershell -NoProfile -ExecutionPolicy unrestricted -Command "$retryCount = 0; $success = $false; $proxyCredentialsRequired = $false; do { try { $wc = New-Object Net.WebClient; if ($proxyCredentialsRequired) { [Net.WebRequest]::DefaultWebProxy.Credentials = [Net.CredentialCache]::DefaultNetworkCredentials; } $wc.DownloadFile('%DOTNET_REMOTE_PATH%', '%DOTNET_LOCAL_PATH%'); $success = $true; } catch { if ($retryCount -ge 6) { throw; } else { $we = $_.Exception.InnerException -as [Net.WebException]; $proxyCredentialsRequired = ($we -ne $null -and ([Net.HttpWebResponse]$we.Response).StatusCode -eq [Net.HttpStatusCode]::ProxyAuthenticationRequired); Start-Sleep -Seconds (5 * $retryCount); $retryCount++; } } } while ($success -eq $false); Add-Type -Assembly 'System.IO.Compression.FileSystem' -ErrorVariable AddTypeErrors; if ($AddTypeErrors.Count -eq 0) { [System.IO.Compression.ZipFile]::ExtractToDirectory('%DOTNET_LOCAL_PATH%', '%DOTNET_PATH%') } else { (New-Object -com shell.application).namespace('%DOTNET_PATH%').CopyHere((new-object -com shell.application).namespace('%DOTNET_LOCAL_PATH%').Items(),16) }" >> "%INIT_TOOLS_LOG%"
if NOT exist "%DOTNET_LOCAL_PATH%" (
  echo ERROR: Could not install dotnet cli correctly. 1>&2
  goto :error
)

:afterdotnetrestore

if exist "%BUILD_TOOLS_PATH%" goto :afterbuildtoolsrestore
echo Restoring BuildTools version %BUILDTOOLS_VERSION%...
echo Running: "%DOTNET_CMD%" restore "%INIT_TOOLS_RESTORE_PROJECT%" --no-cache --packages "%PACKAGES_DIR%" --source "%BUILDTOOLS_SOURCE%" /p:BuildToolsPackageVersion=%BUILDTOOLS_VERSION% /p:ToolsDir=%TOOLRUNTIME_DIR% >> "%INIT_TOOLS_LOG%"
call "%DOTNET_CMD%" restore "%INIT_TOOLS_RESTORE_PROJECT%" --no-cache --packages "%PACKAGES_DIR%" --source "%BUILDTOOLS_SOURCE%" /p:BuildToolsPackageVersion=%BUILDTOOLS_VERSION% /p:ToolsDir=%TOOLRUNTIME_DIR% >> "%INIT_TOOLS_LOG%"
if NOT exist "%BUILD_TOOLS_PATH%\init-tools.cmd" (
  echo ERROR: Could not restore build tools correctly. 1>&2
  goto :error
)

:afterbuildtoolsrestore

echo Initializing BuildTools...
echo Running: "%BUILD_TOOLS_PATH%\init-tools.cmd" "%~dp0" "%DOTNET_CMD%" "%TOOLRUNTIME_DIR%" >> "%INIT_TOOLS_LOG%"
call "%BUILD_TOOLS_PATH%\init-tools.cmd" "%~dp0" "%DOTNET_CMD%" "%TOOLRUNTIME_DIR%" "%PACKAGES_DIR%" >> "%INIT_TOOLS_LOG%"
set INIT_TOOLS_ERRORLEVEL=%ERRORLEVEL%
if not [%INIT_TOOLS_ERRORLEVEL%]==[0] (
  echo ERROR: An error occured when trying to initialize the tools. 1>&2
  goto :error
)

:: Restore a custom RoslynToolset since we can't trivially update the BuildTools dependency in CoreRT
echo Configurating RoslynToolset...
set ROSLYNCOMPILERS_VERSION=3.0.0-beta4-final
set DEFAULT_RESTORE_ARGS=--no-cache --packages "%PACKAGES_DIR%"
set INIT_TOOLS_RESTORE_ARGS=%DEFAULT_RESTORE_ARGS% --source https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json --source https://api.nuget.org/v3/index.json %INIT_TOOLS_RESTORE_ARGS%
set MSBUILD_PROJECT_CONTENT= ^
 ^^^<Project Sdk=^"Microsoft.NET.Sdk^"^^^> ^
  ^^^<PropertyGroup^^^> ^
    ^^^<TargetFrameworks^^^>netcoreapp1.0;net46^^^</TargetFrameworks^^^> ^
    ^^^<DisableImplicitFrameworkReferences^^^>true^^^</DisableImplicitFrameworkReferences^^^> ^
  ^^^</PropertyGroup^^^> ^
  ^^^<ItemGroup^^^> ^
    ^^^<PackageReference Include=^"Microsoft.Net.Compilers^" Version=^"%ROSLYNCOMPILERS_VERSION%^" /^^^> ^
    ^^^<PackageReference Include=^"Microsoft.NETCore.Compilers^" Version=^"%ROSLYNCOMPILERS_VERSION%^" /^^^> ^
  ^^^</ItemGroup^^^> ^
 ^^^</Project^^^>
set PORTABLETARGETS_PROJECT=%TOOLRUNTIME_DIR%\generated\project.csproj
echo %MSBUILD_PROJECT_CONTENT% > "%PORTABLETARGETS_PROJECT%"
@echo on
call "%DOTNET_CMD%" restore "%PORTABLETARGETS_PROJECT%" %INIT_TOOLS_RESTORE_ARGS%
set RESTORE_PORTABLETARGETS_ERROR_LEVEL=%ERRORLEVEL%
@echo off
if not [%RESTORE_PORTABLETARGETS_ERROR_LEVEL%]==[0] (
  echo ERROR: An error ocurred when running: '"%DOTNET_CMD%" restore "%PORTABLETARGETS_PROJECT%"'. Please check above for more details.
  exit /b %RESTORE_PORTABLETARGETS_ERROR_LEVEL%
)

:: Copy Roslyn Compilers Over to ToolRuntime
Robocopy "%PACKAGES_DIR%\Microsoft.Net.Compilers\%ROSLYNCOMPILERS_VERSION%\." "%TOOLRUNTIME_DIR%\net46\roslyn\." /E

:: Create semaphore file
echo Done initializing tools.
if NOT exist "%BUILD_TOOLS_SEMAPHORE_DIR%" mkdir "%BUILD_TOOLS_SEMAPHORE_DIR%"

:: Save init-tools.cmd's commit hash to the semaphore. After pulling / changes branches,
:: if this file has been altered it will trigger a restore of the build tools
git rev-list -1 HEAD init-tools.cmd > "%BUILD_TOOLS_SEMAPHORE%"
if ERRORLEVEL 1 (
  echo ERROR: Could not determine most recent commit hash for init-tools.cmd
  exit /b 1
)
exit /b 0

:error
echo Please check the detailed log that follows. 1>&2
type "%INIT_TOOLS_LOG%" 1>&2
exit /b 1
