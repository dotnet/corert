_Please ensure that [pre-requisites](prerequisites-for-building.md) are installed for a successful build of the repo._

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
`<repo_root>\bin\tests\package\install`, including the *Microsoft.DotNet.ILCompiler* package built
- Run tests

*Note: Currently, the managed components and tests are executed only for Windows Ubuntu/Mac OSX support is coming soon.*

# Compiling .NET Core Console application to native code using using the ILCompiler you built#

**Note: These instructions are currently not working - fix coming ASAP!**

1. Ensure that you have installed the .NET CLI tools from [here](https://github.com/dotnet/cli/). 
2. In the folder where you have the source file to be compiled (e.g. *HelloWorld.cs*), ensure that corresponding *project.json* file also exists. Here is an example of the same:

>     {
>     	"compilationOptions": {
>     	"emitEntryPoint": true
>     	},
>     	"dependencies": {
>     	"System.Console": "4.0.0-beta-23419",
>     	"Microsoft.NETCore.Runtime": "1.0.1-beta-23419",
>     	"Microsoft.NETCore.ConsoleHost": "1.0.0-beta-23419",
>     	"Microsoft.NETCore.TestHost": "1.0.0-beta-23419"
>     	},
>     	"frameworks": {
>     	"dnxcore50": {}
>     	}
>     }

3. Ensure that you have done a repo build per the instructions above.
4. Extract the contents of `<repo_root>\bin\Product\<OS>.<arch>.<Config>\.nuget\toolchain.<OS>-<Arch>.Microsoft.DotNet.ILCompiler.Development.1.0.0-prerelease` to a folder (e.g. c:\ilc)
5. Native executable will be dropped in `./bin/[configuration]/[framework]/native/[binary name]` folder.

*Note: On Windows, please ensure you have VS 2015 installed to get the native toolset and work within a VS 2015 x64 Native Tools command prompt.*

## Using RyuJIT ##

This approach uses the same code-generator (RyuJIT), as [CoreCLR](https://github.com/dotnet/coreclr), for compiling the application. From the shell/command prompt, issue the following commands to generate the native executable:

    dotnet restore
    dotnet compile --native --ilcpath c:\ilc

In this approach, ILCompiler uses the platform specific linker to perform the final module linking.

## Using CPP codegenerator ##

This approach uses platform specific C++ compiler and linker for compiling/linking the application. 

From the shell/command prompt, issue the following commands to generate the native executable:

    dotnet restore
    dotnet compile --native --cpp --ilcpath c:\ilc