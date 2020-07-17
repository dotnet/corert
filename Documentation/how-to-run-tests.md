# Testing CoreRT

The CoreRT test harness can run in two modes - with the tests local to the CoreRT repo, with tests from the [CoreCLR](https://github.com/dotnet/coreclr) repo or the tests from the [CoreFX](https://github.com/dotnet/corefx) repo. The local tests only provide basic sanity testing and it's recommended to run the CoreCLR and CoreFX tests which are much more thorough.

The tests exercise both the runtime and the ILC compiler, which compiles IL into native code. The harness can test both the RyuJIT code generation backend, or the C++ backend of the ILC compiler.

## Local Tests

Make sure you have the [prerequisites](prerequisites-for-building.md) to build the repo, and run `build.cmd debug clean` at repo root level. This will build the CoreRT repo, compile the local test sources, and use the newly built ILC compiler to compile the tests to native code and run them. These tests also run as part of the CI.

### How To Run

On Windows:
```
cd {corert}
build.cmd
tests\runtest.cmd
```

On Linux / macOS:
```
cd {corert}
./build.sh
tests\runtest.sh
```

If you want to run just single test
```
tests\runtest.cmd /test Pinvoke
```

If you want to run tests only for specific codegen
```
tests\runtest.cmd /test Pinvoke /mode Jit
```


### Verifying tests pass
You should see the below message when you build CoreRT or run the local tests manually, otherwise something is broken.

```
JIT - TOTAL: 12 PASSED: 12
CPP - TOTAL: 2 PASSED: 2
WASM - TOTAL: 1 PASSED: 1
```

## External Tests - CoreCLR

When runtest.cmd is passed the /coreclr switch, the harness will download the CoreCLR project's test suite, compile them to native with the CoreRT compiler, and run them.

### How To Run

Choose the set of tests you want to run. Currently the options are:

* Top200
  * Small set of the suite selected to provide broad coverage quickly (Under 10 minutes). These run as part of the CI when submitting a pull request.
* KnownGood
  * Subset of the suite previously validated to all pass on CoreRT. If these all pass you can be pretty sure you haven't regressed the compiler. We currently only have a KnownGood list on Windows.
* All
  * The entire suite. Many of the tests will fail since CoreRT is still pre-release and some tests don't play well with an ahead-of-time compiler.

On Windows:

```
tests\runtest.cmd /coreclr Top200|All|KnownGood
```

On Linux / macOS:

```
tests/runtest.sh -coreclr Top200|All|KnownGood
```

### Suppress Windows Error Reporting Dialogs

It's advisable to use some sort of a dialog killer tool if you see test regressions as many tests fail with pop-ups for Windows Error Reporting. However, the following regedit scripts have also proven to be useful to mask these pop-ups.

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

### Filtering Tests

If you know a test is failing for a good reason or you want a clean baseline, please use ```corert\tests\KnownGood.CoreCLR.issues.targets``` to weed out bad tests or infrastructure errors until we fix them.

### Test Logs

When the tests finish execution, the log location will be written out and should be in bin\Logs:

**Example:** ```corert\bin\Logs\TestRun_Windows_NT__x64__debug.html```

## External tests - CoreFX

Similarly to the CoreCLR tests, when runtest.cmd is passed the /corefx switch, the harness will download the CoreFX project's test suite, compile them to native with the CoreRT compiler, and run them.

### How to run

To run CoreFX tests on CoreRT, make sure that `build.cmd` has been run at least once in the configuration you'd like to test (i.e. `Debug` or `Release`), open a new console window and from the repo root execute the following:

On Windows:

```
tests\runtest.cmd /corefx
```

On Linux / macOS:

```
tests/runtest.sh -corefx
```

The tests assemblies to run are defined in `TopN.CoreFX.[Windows/Unix].issues.json` with their respectively excluded test methods, classes or namespaces.

### Reproducing test failures

If you need to reproduce a failing test, navigate to ```test_downloaded\CoreFX``` and then to the folder of the failing test - each test suite is located in its own folder. From the test suite directory run the following:

On Windows:

```
.\native\xunit.console.netcore.exe .\<name of the main test assembly>  @"./<name of the main test assembly>.rsp" -notrait category=nonnetcoreapptests -notrait category=nonwindowstests  -notrait category=failing
```

On Linux / macOS:

```
./native/xunit.console.netcore.exe ./<name of the main test assembly>  @"./<name of the main test assembly>.rsp" -notrait category=nonnetcoreapptests -notrait category=failing
```

Additionally for Linux, add `-notrait category=nonlinuxtests` and for macOS `-notrait category=nonosxtests`.

**e.g.** for System.Collections.Tests on Windows:

Navigate to C:\repos\corert\test_downloaded\CoreFX\System.Collections.Tests. 
Open Command Promps and run:

```
.\native\xunit.console.netcore.exe .\System.Collections.Tests.dll  `@"System.Collections.Tests.rsp" -notrait category=nonnetcoreapptests -notrait category=nonwindowstests  -notrait category=failing
```

### Enabling tests

To enable a new CoreFX test project to run against CoreRT add its fully qualified name to `TopN.CoreFX.[Windows/Unix].issues.json`.
To remove a test from a test project which is already enabled, in the same file find and delete the definition containing its name.

### Disabling tests

Tests can be excluded from a run in the following ways:

* To exclude a specific test method, add its fully-qualified name in the `method` array of the `exclusions` attribute of relevant test project or pass it as a value to the `-skipmethod` flag when calling `xunit.console.netcore.exe`.

* To exclude all tests in a class, add  its fully-qualified name in the `class` array of the `exclusions` attribute of relevant test project or pass it as a value to the `-skipclass` flag when calling `xunit.console.netcore.exe`.

* To exclude all tests in a class, add  its fully-qualified name in the `namespace` array of the `exclusions` attribute of relevant test project or pass it as a value to the `-skipnamespace` flag when calling `xunit.console.netcore.exe`.
