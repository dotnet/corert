_Please ensure that [pre-requisites](Pre-requisites-for-Building.md) are installed for a successful build of the repo._

_Note_:

* Instructions below assume C:\corert is the repo root.
* Build the repo using build.cmd. Binaries are binplaced to ```<repo_root>\bin\Product\<OS>.<arch>.<Config>```

# Build ILCompiler #

Build your repo by issuing the following command at repo root:

> build[.cmd|.sh] clean [Debug|Release]

This will result in the following:

- Build native and managed components of ILCompiler
- Build tests
- Restore dependent nuget packages into
`<repo_root>\bin\tests\package\install`, including the ILCompiler package built
- Run tests

Note: Currently, the managed components and tests are executed only for Windows. Enabling them for Linux and Mac OSX is tracked by [GH Issue 146](https://github.com/dotnet/corert/issues/146).

# Compiling MSIL to native code #

Ensure that you have built the MSIL version of the assembly targeted for .NET Core. The Compilation process involves using the **dotnet-compile-native** [CLI](https://github.com/dotnet/cli) command, with explicitly specifying the various arguments. The general form of the command is as follows:

    dotnet-compile-native <arch> <build type> [one or more options]

- **arch** is currently supported to be *x64*.
- **Build Type** can be *Debug* or *Release*.

Here are the various options supported by the command - each option is space separated by its value:

- **/in** *absolute path to MSIL assembly to be compiled*. Required.
- **/out** *absolute path to the native executable to be generated*. Required.
- **/mode** *the compilation mode and can be cpp (default) or protojit*. Optional.
- **/appdepsdk** *absolute path to the folder where Microsoft.DotNet.AppDep package was restored*. Required.
- **/codegenpath** *absolute path to the folder where Microsoft.DotNet.ProtoJit package was restored*. Specify only when mode is protojit.
- **/objgenpath** *absolute path to the folder where Microsoft.DotNet.ObjectWriter package was restored*. Specify only when mode is protojit.
- **/linklibs** *Additional import libraries to be specified to the platform linker. Multiple libraries should be enclosed within double-quotes and space separated.* Optional.


## Using non-CPP codegenerator (e.g. ProtoJIT) ##

Issue the following command to compile
> *reporoot*\bin\tests\package\install\toolchain.Windows_NT-x64.Microsoft.DotNet.ILCompiler.Development.1.0.0-prerelease\dotnet-compile-native.bat **x64** **debug** **/appdepsdk** *reporoot*\bin\tests\package\install\Microsoft.DotNet.AppDep.1.0.0-prerelease **/mode** protojit **/objgenpath** *reporoot*\bin\tests\package\install\Microsoft.DotNet.ObjectWriter.1.0.0-prerelease **/codegenpath** *reporoot*\bin\tests\package\install\Microsoft.DotNet.ProtoJit.1.0.0-prerelease **/in** d:\gh\compile\app\repro.exe **/out** d:\gh\compile\app\rnjit.exe

You will see the output below:

    Generating app obj file
    Generating native executable
    Build successfully completed.

    d:\GH\compile\app>rnjit.exe
    Hello world


## Using CPP codegenerator ##


Issue the following command to compile
> *reporoot*\bin\tests\package\install\toolchain.Windows_NT-x64.Microsoft.DotNet.ILCompiler.Development.1.0.0-prerelease\dotnet-compile-native.bat **x64** **debug** **/appdepsdk** *reporoot*\bin\tests\package\install\Microsoft.DotNet.AppDep.1.0.0-prerelease **/mode** cpp **/in** d:\gh\compile\app\repro.exe **/out** d:\gh\compile\app\rncpp.exe

You will see the output below:

    Generating source file
    Compiling application source files
    Generating native executable
    Build successfully completed.

    d:\GH\compile\app>rncpp.exe
    Hello world
