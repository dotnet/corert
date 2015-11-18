# Known Issues

## Windows

During dotnet-compile-native, when using VS 2015 RTM,
* LINK : fatal error LNK1101: incorrect MSPDB140.DLL version; recheck installation of this product

If you are using VS 2015 Update 1, no action is needed.
* Please use the workaround [here](https://connect.microsoft.com/VisualStudio/feedback/details/1651822/incorrect-mspdb140-dll-version-picked-in-x86-x64-cross-tools-environment).

# Pre Checkin Tests

## On Windows

```build.cmd debug clean```
at repo root level with your changes before you merge your PR. These tests run as part of the CI.

### Dependencies

* Visual Studio Dev 15 must be installed to use the platform linker at ```%VS140COMNTOOLS%```

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
* Clone (or pull into) repo: dotnet/coreclr into coreclr
* Clone (or pull into) repo: dotnet/corert into corert
* Open a new command prompt:

> cd corert
> build.cmd
> cd tests
> runtest.cmd /?
> 
> runtest.cmd [OS] [arch] [flavor] [/extrepo] [/buildextrepo] [/mode] [/runtest]
> /mode : Compilation mode. Specify cpp/RyuJIT. Default: RyuJIT
> /runtest  : Should just compile or run compiled bianry? Specify: true/false. Default: true.
> /extrepo  : Path to external repo, currently supports: GitHub: dotnet/coreclr. Specify full path. If unspecified, runs corert tests
> /buildextrepo : Should build at root level of external repo? Specify: true/false. Default: true
> /nocache  : When restoring toolchain packages, obtain them from the feed not the cache.

## External Repo (CoreCLR Testing)
**Test ILToNative compilation only**

```runtest.cmd /runtest false /extrepo e:\git\coreclr /buildextrepo false```

**Test ILToNative RyuJIT Compilation and Run Exe**

```runtest.cmd /mode protojit /runtest true /extrepo e:\git\coreclr /buildextrepo false```

**Test ILToNative CPP Compilation and Run Exe**

```runtest.cmd /mode cpp /runtest true /extrepo e:\git\coreclr /buildextrepo false```

**Restore Packages from NuGet with Nocache**

```runtest.cmd /nocache```

## CoreRT Testing
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
