_Please ensure that [pre-requisites](prerequisites-for-building.md) are installed for a successful build of the repo._

# Build ILCompiler #

Build your repo by issuing the following command at repo root:

```
build[.cmd|.sh] clean [Debug|Release]
```

This will result in the following:

- Restore nuget packages required for building
- Build native and managed components of ILCompiler. The final binaries are placed to `<repo_root>\bin\<OS>.<arch>.<Config>\tools`.
- Build and run tests

## On Windows

- You should use `x64 Native Tools Command Prompt for VS 2019`

If you have both stable and preview VS versions installed, open corresponding command prompt where you have C++ tools installed.

# Install .NET Core 3.1 SDK

Download .NET Core 3.1 SDK from [https://www.microsoft.com/net/download/core](https://www.microsoft.com/net/download/core)

You should now be able to use the `dotnet` commands of the CLI tools.

# Compiling source to native code using the ILCompiler you built #

* Ensure that you have done a repo build per the instructions above.
* Create a new folder and switch into it. 
* Run `dotnet new console` on the command/shell prompt. This will add a project template. If you get an error, please ensure the [pre-requisites](prerequisites-for-building.md) are installed. 
* Modify `.csproj` file that is part of your project. **Important:** A few lines at the top and at the bottom are different from the default template - don't miss updating them!

```
<Project>
  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp3.1</TargetFramework>
  </PropertyGroup>

  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
  <Import Project="$(IlcPath)\build\Microsoft.NETCore.Native.targets" />
</Project>
```

* Set IlcPath environment variable to point to the built binaries. Alternatively, pass an extra `/p:IlcPath=<repo_root>\bin\Windows_NT.x64.Debug` argument to all dotnet commands below. **Important:** Use of the `IlcPath` variable is required as the target files rely on it, do not replace the variable in the `.csproj` by the full path or you will encounter errors during publish.

    * Unix: `export IlcPath=<repo_root>/bin/Linux.x64.Debug`

    * Windows: `set IlcPath=<repo_root>\bin\Windows_NT.x64.Debug`

* Please [open an issue](https://github.com/dotnet/corert/issues) if these instructions do not work anymore.

## Using RyuJIT ##

This approach uses the same code-generator (RyuJIT), as [CoreCLR](https://github.com/dotnet/runtime), for compiling the application. Linking is done using the platform specific linker.

From the shell/command prompt, issue the following commands, from the folder containing your project, to generate the native executable

``` 
    dotnet publish -r win-x64|linux-x64|osx-x64 
``` 

Native executable will be dropped in `./bin/x64/[configuration]/netcoreapp2.1/publish/` folder and will have the same name as the folder in which your source file is present.

## Using CPP Code Generator ##

This approach uses [transpiler](https://en.wikipedia.org/wiki/Source-to-source_compiler) to convert IL to C++, and then uses platform specific C++ compiler and linker for compiling/linking the application. The transpiler is a lot less mature than the RyuJIT path. If you came here to give CoreRT a try on your .NET Core program, use the RyuJIT option above.

From the shell/command prompt, issue the following commands to generate the native executable:

``` 
    dotnet publish /p:NativeCodeGen=cpp -r win-x64|linux-x64|osx-x64 
```

For CoreRT debug build on Windows, add an extra `/p:AdditionalCppCompilerFlags=/MTd` argument.

## Disabling Native Compilation ##

Native compilation can be disabled during publishing by adding an extra `/p:NativeCompilationDuringPublish=false` argument.

## Workarounds for build errors on Windows ##

If you are seeing errors such as:

```
    LNK2038: mismatch detected for 'RuntimeLibrary': value 'MTd_StaticDebug' doesn't match value 'MT_StaticRelease'
```

- Make sure to use release build, or pass the extra `/p:AdditionalCppCompilerFlags=/MTd` argument above.

If you are seeing errors such as:

```
libcpmtd.lib(nothrow.obj) : fatal error LNK1112: module machine type 'X86' conflicts with target machine type 'x64' [C:\Users\[omitted]\nativetest\app\app.csproj]
C:\Users\[omitted]\nativetest\bin\Windows_NT.x64.Debug\build\Microsoft.NETCore.Native.targets(151,5): error MSB3073: The command "link  @"obj\Debug\netcoreapp3.1\native\link.rsp"" exited with code 1112. [C:\Users\[omitted]\nativetest\app\app.csproj]
```

or 

```
Microsoft.NETCore.Native.targets(150,5): error MSB3073: The command "link  @"native\link.rsp"" exited with code 1.
```

or

```
Microsoft.NETCore.Native.targets(132,5): error MSB3073: The command "cl @"native\cl.rsp"" exited with code 9009.
```

Make sure you run these commands from the `x64 Native Tools Command Prompt for VS 2019` instead of a vanilla command prompt

For more details see discussion in issue #2679
