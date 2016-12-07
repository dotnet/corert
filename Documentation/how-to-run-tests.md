# Testing CoreRT

The CoreRT test harness can run in two modes - with the tests local to the CoreRT repo, or with tests from the [CoreCLR](http://github.com/dotnet/coreclr) repo. The local tests only provide basic sanity testing and it's recommended to run the CoreCLR tests which are much more thorough.

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

### Verifying tests pass
You should see the below message when you build CoreRT or run the local tests manually, otherwise something is broken.

```
JIT - TOTAL: 7 PASSED: 7
CPP - TOTAL: 2 PASSED: 2
```

## External Tests

*Note: These are currently supported only on Windows and Ubuntu/macOS support is coming soon.*

When runtest.cmd is passed the /coreclr switch, the harness will download the CoreCLR project's test suite, compile them to native with the CoreRT compiler, and run them.

### How To Run

Choose the set of tests you want to run. Currently the options are:
* Top200
    * Small set of the suite selected to provide broad coverage quickly (Under 10 minutes). These run as part of the CI when submitting a pull request.
* KnownGood
    * Subset of the suite previously validated to all pass on CoreRT. If these all pass you can be pretty sure you haven't regressed the compiler.
* All
    * The entire suite. Many of the tests will fail since CoreRT is still pre-release and some tests don't play well with an ahead-of-time compiler.

On Windows:
```
tests\runtest.cmd /coreclr Top200|All|KnownGood
```

On Linux / macOS:

**TBD**

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
