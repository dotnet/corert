# Testing CoreRT

The CoreRT test harness can run in two modes - with the tests local to the CoreRT repo, or with tests from the [CoreCLR](http://github.com/dotnet/coreclr) repo. The local tests only provide basic sanity testing and it's recommended to run the CoreCLR tests which are much more thorough.

The tests are exercising both the runtime, and the ILC compiler that compiles IL into native code. The harness can test both the RyuJIT code generation backend, or the C++ backend of the ILC compiler.

## On Windows

Make sure you have the [prerequisites](prerequisites-for-building.md) to build the repo, and run `build.cmd debug clean` at repo root level. This will build the CoreRT repo, compile the local test sources, and use the built ILC compiler to compile the tests to native code and run them. These tests run as part of the CI.

### Verifying test pass
You should see the below message when you run the above ```build.cmd```, else something is broken.

```:
:
Running test Hello
Hello: pass

TOTAL: 3 PASSED: 3
```

# External Tests

*Note: These are currently supported only on Windows and Ubuntu/Mac OSX support is coming soon.*

## Setup
* Clone (or pull into) repo: [dotnet/coreclr](http://github.com/dotnet/coreclr) into {coreclr}
* Clone (or pull into) repo: dotnet/corert into {corert}
* Open a new command prompt:

```
cd {corert}
build.cmd
cd tests
runtest.cmd /?

runtest.cmd [OS] [arch] [flavor] [/extrepo] [/buildextrepo] [/mode] [/runtest]
/mode : Compilation mode. Specify cpp/RyuJIT. Default: RyuJIT
/runtest  : Should just compile or run compiled bianry? Specify: true/false. Default: true.
/extrepo  : Path to external repo, currently supports: GitHub: dotnet/coreclr. Specify full path. If unspecified, runs corert tests
/buildextrepo : Should build at root level of external repo? Specify: true/false. Default: true
/nocache  : When restoring toolchain packages, obtain them from the feed not the cache.
```

## External Repo (CoreCLR Testing)
At this point, running CoreCLR tests is known to succeed in RyuJIT. We haven't done failure bucketing for the C++ backend and you'll probably not get a clean test pass.

**Test ILC compilation but don't run the tests**

```
runtest.cmd /runtest false /extrepo e:\git\coreclr
```

**Test ILToNative RyuJIT Compilation and Run Exe**

```
runtest.cmd /mode ryujit /runtest true /extrepo e:\git\coreclr
```

**Test ILToNative CPP Compilation and Run Exe**

```
runtest.cmd /mode cpp /runtest true /extrepo e:\git\coreclr
```

**Restore Packages from NuGet with Nocache**

```
runtest.cmd /nocache
```

After you initially build the CoreCLR repo, it's recommended to pass `/buildextrepo false` to runtests so that you skip the part where the tests are built from sources into IL. This step doesn't require testing.


## Running local CoreRT tests
```build.cmd``` auto runs the pre-checkin tests with RyuJIT

**Run CoreRT pre-checkin tests in CPP mode**

```runtest.cmd /mode cpp```

## Test Run (Failure Dialogs)
You need some sort of a dialog killer tool as many tests fail with pop-ups for Windows Error Reporting. However, the following regedit scripts have also proven to be useful to mask these pop-ups.

Disable WER *temporarily*:

**Contents of disable-wer.reg**
```
REGEDIT4

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Windows Error Reporting]
"DontShowUI"="1"
"Disabled"="1"
```

Remember to enable:

**Contents of enable-wer.reg**
```
REGEDIT4

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Windows Error Reporting]
"DontShowUI"="0"
"Disabled"=-
```

## Filtering Tests
If you know a test is failing for a good reason or you want a clean baseline, please use the ```corert\tests\CoreCLR.issues.targets``` to weed out bad tests or infrastructure errors until we fix them.

## Test Logs
Finally, to see the test reports, the process is to look for them in CoreCLR test drop location.

**Example:** ```coreclr\bin\Logs\TestRun_Windows_NT__x64__debug.html``` and ```coreclr\bin\tests\Windows_NT.x64.Debug\Reports```
