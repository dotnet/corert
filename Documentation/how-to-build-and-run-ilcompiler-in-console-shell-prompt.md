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
- Installs the latest CLI tools at `<repo_root>\Tools\dotnetcli`

# Setup CLI
To consume the CLI tools installed as part of the build, do the following:

* Add `<repo_root>\Tools\dotnetcli` to the path
* On windows ensure you are using the 'VS2015 x64 Native Tools Command Prompt'
    (This is distinct from the 'Developer Command Prompt for VS2015')

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
    dotnet build --native --ilcpath <repo_root>\bin\Product\Windows_NT.x64.Debug\packaging\publish1
``` 

Native executable will be dropped in `./bin/[configuration]/[framework]/[runtime]/native/` folder and will have the same name as the folder in which your source file is present.

## Using CPP Code Generator ##

This approach uses platform specific C++ compiler and linker for compiling/linking the application.

From the shell/command prompt, issue the following commands to generate the native executable:

``` 
    dotnet restore
    dotnet build --native --cpp --ilcpath <repo_root>\bin\Product\Windows_NT.x64.Debug\packaging\publish1 --cppcompilerflags /MTd
```

Omit `--cppcompilerflags /MTd` for release CoreRT build.

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
