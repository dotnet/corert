# Building a Hello World console app with CoreRT

CoreRT is an AOT-optimized .NET Core runtime. This document will guide you through compiling a .NET Core Console application with CoreRT.

_Please ensure that [pre-requisites](../prerequisites.md) are installed._

## Create .NET Core Console project
Open a new shell/command prompt window and run the following commands.
```bash
> dotnet new console -o HelloWorld
> cd HelloWorld
```

This will create a simple Hello World console app in `Program.cs` and associated project files.

## Add CoreRT to your project
To use CoreRT with your project, you need to add a reference to the ILCompiler NuGet package that contains the CoreRT ahead of time compiler and runtime.
For the compiler to work, it first needs to be added to your project.

In your shell/command prompt navigate to the root directory of your project and run the command:

```bash
> dotnet new nuget 
```

This will add a nuget.config file to your application. Open the file and in the ``<packageSources> `` element under ``<clear/>`` add the following:

```xml
<add key="dotnet-core" value="https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json" />
<add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
```

Once you've added the package source, add a reference to the compiler by running the following command:

```bash
> dotnet add package Microsoft.DotNet.ILCompiler -v 1.0.0-alpha-* 
```

## Restore and Publish your app

Once the package has been successfully added it's time to compile and publish your app! In the shell/command prompt window, run the following command:

```bash
> dotnet publish -r <RID> -c <Configuration>
```

where `<Configuration>` is your project configuration (such as Debug or Release) and `<RID>` is the runtime identifier (one of win-x64, linux-x64, osx-x64). For example, if you want to publish a release configuration of your app for a 64-bit version of Windows the command would look like:

```bash 
> dotnet publish -r win-x64 -c release
```

Once completed, you can find the native executable in the root folder of your project under `/bin/x64/<Configuration>/netcoreapp2.1/publish/`. Navigate to `/bin/x64/<Configuration>/netcoreapp2.1/publish/` in your project folder and run the produced native executable.

Feel free to modify the sample application and experiment. However, keep in mind some functionality might not yet be supported in CoreRT. Let us know on the [Issues page](https://github.com/dotnet/corert/issues/).
