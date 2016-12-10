_Please ensure that [pre-requisites](prerequisites-for-building.md) are installed for a successful build of the repo._

# Build ILCompiler #

Build your repo by issuing the following command at repo root:

```
build[.cmd|.sh] clean [Debug|Release]
```

This will result in the following:

- Restore nuget packages required for building
- Build native and managed components of ILCompiler. The final binaries are placed to `<repo_root>\bin\Product\<OS>.<arch>.<Config>\packaging\publish1`.
- Build and run tests

# Install latest CLI tools

* Download latest CLI tools from [https://github.com/dotnet/cli/](https://github.com/dotnet/cli/) and add them to the path. The latest CLI tools include MSBuild support that the native compilation build integration depends on.
* On windows ensure you are using the 'VS2015 x64 Native Tools Command Prompt'
    (This is distinct from the 'Developer Command Prompt for VS2015')

You should now be able to use the `dotnet` commands of the CLI tools.

# Compiling source to native code using the ILCompiler you built#

* Ensure that you have done a repo build per the instructions above.
* Create a new folder and switch into it. 
* Run `dotnet new` on the command/shell prompt. This will add a project template. If you get an error, please ensure the [pre-requisites](prerequisites-for-building.md) are installed. 
* Modify `.csproj` file that is part of your project. A few lines at the top and at the bottom are diferent from the default template.

```
<Project ToolsVersion="15.0">
  <Import Project="$(MSBuildSDKsPath)\Microsoft.NET.Sdk\Sdk\Sdk.props" />

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp1.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="**\*.cs" />
    <EmbeddedResource Include="**\*.resx" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NETCore.App" Version="1.0.1" />
  </ItemGroup>

  <Import Project="$(MSBuildSDKsPath)\Microsoft.NET.Sdk\Sdk\Sdk.targets" />
  <Import Project="$(IlcPath)\Microsoft.NETCore.Native.targets" />
</Project>

```

* Set IlcPath environment variable to point to the built binaries. Alternatively, pass an extra `/p:IlcPath=<repo_root>\bin\Product\Windows_NT.x64.Debug\packaging\publish1` argument to all dotnet commands below.

```
    set IlcPath=<repo_root>\bin\Product\Windows_NT.x64.Debug\packaging\publish1
```

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
    dotnet build /t:LinkNative /p:NativeCodeGen=cpp /p:AdditionalCppCompilerFlags=/MTd
```

Omit `/p:AdditionalCppCompilerFlags=/MTd` for release CoreRT build.

## Workarounds for linker errors on Windows ##

If you are seeing errors such as: 

```
    LINK : fatal error LNK1104: cannot open file 'kernel32.lib'
    Linking of intermediate files failed.
```

There are a few workarounds you might try:
 - Make sure you run these commands from the 'VS2015 x64 Native Tools Command Prompt' instead of a vanilla command prompt
 - Search for the missing lib files in your SDK, for example under C:\Program Files (x86)\Windows Kits\10\lib. Make sure the path to these libraries is included in the LIB environment variable. It appears VS 2015 RTM developer command prompt does not correctly set the LIB paths for the 10586 Windows SDK. VS 2015 Update 1 resolves that issue, so installing it is another alternative.

For more details see the discussion in issue #606
