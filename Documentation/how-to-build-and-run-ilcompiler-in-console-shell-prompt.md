_Please ensure that [pre-requisites](prerequisites-for-building.md) are installed for a successful build of the repo._

# Build ILCompiler #

Build your repo by issuing the following command at repo root:

```
build[.cmd|.sh] clean [Debug|Release]
```

If you're using Visual Studio 2017, you need to run the above command from the "Developer Command Prompt for VS 2017". Visual Studio setup puts a shortcut to this in the Start menu.

This will result in the following:

- Restore nuget packages required for building
- Build native and managed components of ILCompiler. The final binaries are placed to `<repo_root>\bin\Product\<OS>.<arch>.<Config>\packaging\publish1`.
- Build and run tests

# Install latest CLI tools

* Download latest CLI tools from [https://github.com/dotnet/cli/](https://github.com/dotnet/cli/) and add them to the path. The latest CLI tools include MSBuild support that the native compilation build integration depends on. These instructions have been tested with build `1.0.0-rc4-004812`.
* On windows ensure you are using the 'x64 Native Tools Command Prompt for VS 2017' or 'VS2015 x64 Native Tools Command Prompt'
    (This is distinct from the 'Developer Command Prompt for VS 2017')

You should now be able to use the `dotnet` commands of the CLI tools.

# Compiling source to native code using the ILCompiler you built #

* Ensure that you have done a repo build per the instructions above.
* Create a new folder and switch into it. 
* Run `dotnet new` on the command/shell prompt. This will add a project template. If you get an error, please ensure the [pre-requisites](prerequisites-for-building.md) are installed. 
* Modify `.csproj` file that is part of your project. A few lines at the top and at the bottom are different from the default template.

```
<Project>
  <Import Project="$(MSBuildSDKsPath)\Microsoft.NET.Sdk\Sdk\Sdk.props" />

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp1.0</TargetFramework>
  </PropertyGroup>

  <Import Project="$(MSBuildSDKsPath)\Microsoft.NET.Sdk\Sdk\Sdk.targets" />
  <Import Project="$(IlcPath)\Microsoft.NETCore.Native.targets" />
</Project>
```

* Set IlcPath environment variable to point to the built binaries. Alternatively, pass an extra `/p:IlcPath=<repo_root>\bin\Product\Windows_NT.x64.Debug\packaging\publish1` argument to all dotnet commands below.

    * Unix: `export IlcPath=<repo_root>/bin/Product/Linux.x64.Debug/packaging/publish1`

    * Windows: `set IlcPath=<repo_root>\bin\Product\Windows_NT.x64.Debug\packaging\publish1`

* Run `dotnet restore`. This will download nuget packages required for compilation.

* Please [open an issue](https://github.com/dotnet/corert/issues) if these instructions do not work anymore. .NET Core integration with MSBuild is work in progress and these instructions need updating accordingly from time to time.

## Using RyuJIT ##

This approach uses the same code-generator (RyuJIT), as [CoreCLR](https://github.com/dotnet/coreclr), for compiling the application. Linking is done using the platform specific linker.

From the shell/command prompt, issue the following commands, from the folder containing your project, to generate the native executable

``` 
    dotnet build /t:LinkNative
``` 

Native executable will be dropped in `./bin/[configuration]/native/` folder and will have the same name as the folder in which your source file is present.

## Using CPP Code Generator ##

This approach uses platform specific C++ compiler and linker for compiling/linking the application.

From the shell/command prompt, issue the following commands to generate the native executable:

``` 
    dotnet build /t:LinkNative /p:NativeCodeGen=cpp
```

For CoreRT debug build on Windows, add an extra `/p:AdditionalCppCompilerFlags=/MTd` argument.

## Workarounds for build errors on Windows ##

If you are seeing errors such as:

```
    LNK2038: mismatch detected for 'RuntimeLibrary': value 'MTd_StaticDebug' doesn't match value 'MT_StaticRelease'
```

- Make sure to use release build, or pass the extra `/p:AdditionalCppCompilerFlags=/MTd` argument above.

If you are seeing errors such as:

```
libcpmtd.lib(nothrow.obj) : fatal error LNK1112: module machine type 'X86' conflicts with target machine type 'x64' [C:\Users\[omitted]\nativetest\app\app.csproj]
C:\Users\[omitted]\nativetest\bin\Product\Windows_NT.x64.Debug\packaging\publish1\Microsoft.NETCore.Native.targets(151,5): error MSB3073: The command "link  @"obj\Debug\netcoreapp1.0\native\link.rsp"" exited with code 1112. [C:\Users\[omitted]\nativetest\app\app.csproj]
```

Make sure you run these commands from the `x64 Native Tools Command Prompt for VS 2017` instead of a vanilla command prompt

For more details see discussion in issue #2679
