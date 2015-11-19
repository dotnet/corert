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
exit /b 0

:Usage
echo %0 [arch: x64/x86/arm] [flavor: debug/release]
exit /b 1
