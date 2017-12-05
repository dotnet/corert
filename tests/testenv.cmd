@if not defined _echo @echo off

:Arg_Loop
if "%1" == "" goto :ArgsDone
if /i "%1" == "/?" goto :Usage
if /i "%1" == "x64"     (set CoreRT_BuildArch=x64&&shift&goto Arg_Loop)
if /i "%1" == "x86"     (set CoreRT_BuildArch=x86&&shift&goto Arg_Loop)
if /i "%1" == "arm"     (set CoreRT_BuildArch=arm&&shift&goto Arg_Loop)
if /i "%1" == "wasm"    (set CoreRT_BuildArch=wasm&&shift&goto Arg_Loop)

if /i "%1" == "dbg"     (set CoreRT_BuildType=Debug&shift&goto Arg_Loop)
if /i "%1" == "debug"   (set CoreRT_BuildType=Debug&shift&goto Arg_Loop)
if /i "%1" == "rel"     (set CoreRT_BuildType=Release&shift&goto Arg_Loop)
if /i "%1" == "release" (set CoreRT_BuildType=Release&shift&goto Arg_Loop)

:ArgsDone

set CoreRT_BuildOS=Windows_NT

if /i "%CoreRT_BuildArch%" == "wasm" (set CoreRT_BuildOS=WebAssembly)

set CoreRT_ToolchainDir=%~dp0\..\bin\%CoreRT_BuildOS%.%CoreRT_BuildArch%.%CoreRT_BuildType%

exit /b 0

:Usage
echo %0 [arch: x64/x86/arm/wasm] [flavor: debug/release]
exit /b 1
