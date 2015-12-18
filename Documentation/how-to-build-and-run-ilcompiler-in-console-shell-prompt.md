_Please ensure that [pre-requisites](prerequisites-for-building.md) are installed for a successful build of the repo._

# Build ILCompiler #

Build your repo by issuing the following command at repo root:

```
build[.cmd|.sh] clean [Debug|Release]
```

This will result in the following:

- Restore nuget packages required for building
- Build native and managed components of ILCompiler. The final binaries are placed to `<repo_root>\bin\Product\<OS>.<arch>.<Config>\.nuget\publish1`.
- Build and run tests
- Installs the latest CLI tools at `<repo_root>\bin\tools\cli`

# Setup CLI
To consume the CLI tools installed as part of the build, do the following:

* Add `<repo_root>\bin\tools\cli\bin` to the path
* set `DOTNET_HOME` environment variable to `<repo_root>\bin\tools\cli`

You should now be able to use the `dotnet` commands of the CLI tools.

# Compiling source to native code using the ILCompiler you built#

* Ensure that you have done a repo build per the instructions above.
* Create a new folder and switch into it. 
* Issue the command, `dotnet new`, on the command/shell prompt. This will add a template source file and corresponding project.json. If you get an error, please ensure the [pre-requisites](prerequisites-for-building.md) are installed. 


## Using RyuJIT ##

This approach uses the same code-generator (RyuJIT), as [CoreCLR](https://github.com/dotnet/coreclr), for compiling the application. Linking is done using the platform specific linker.

From the shell/command prompt, issue the following commands, from the folder containing your source file and project.json, to generate the native executable

``` 
    dotnet restore
    dotnet compile --native --ilcpath bin\Product\Windows_NT.x64.Debug\.nuget\publish1
``` 

Native executable will be dropped in `./bin/[configuration]/[framework]/native/` folder and will have the same name as the folder in which your source file is present.

## Using CPP Code Generator ##

This approach uses platform specific C++ compiler and linker for compiling/linking the application.

From the shell/command prompt, issue the following commands to generate the native executable:

``` 
    dotnet restore
    dotnet compile --native --cpp --ilcpath bin\Product\Windows_NT.x64.Debug\.nuget\publish1
```
