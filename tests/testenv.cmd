@echo off

:Arg_Loop
if "%1" == "" goto :ArgsDone
if /i "%1" == "/?" goto :Usage
if /i "%1" == "x64"     (set CoreRT_BuildArch=x64&&shift&goto Arg_Loop)
if /i "%1" == "x86"     (set CoreRT_BuildArch=x86&&shift&goto Arg_Loop)
if /i "%1" == "arm"     (set CoreRT_BuildArch=arm&&shift&goto Arg_Loop)

if /i "%1" == "dbg"     (set CoreRT_BuildType=Debug&shift&goto Arg_Loop)
if /i "%1" == "debug"   (set CoreRT_BuildType=Debug&shift&goto Arg_Loop)
if /i "%1" == "rel"     (set CoreRT_BuildType=Release&shift&goto Arg_Loop)
if /i "%1" == "release" (set CoreRT_BuildType=Release&shift&goto Arg_Loop)

:ArgsDone

set CoreRT_BuildOS=Windows_NT

set CoreRT_AppDepSdkPkg=toolchain.win7-%CoreRT_BuildArch%.Microsoft.DotNet.AppDep
set CoreRT_AppDepSdkVer=1.0.6-prerelease-00004

set CoreRT_ToolchainDir=%~dp0\..\bin\Product\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%\packaging\publish1
set CoreRT_AppDepSdkDir=%~dp0\..\packages\%CoreRT_AppDepSdkPkg%\%CoreRT_AppDepSdkVer%

exit /b 0

:Usage
echo %0 [arch: x64/x86/arm] [flavor: debug/release]
exit /b 1
