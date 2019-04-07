# Building a MonoGame app with CoreRT

CoreRT is an AOT-optimized .NET Core runtime. This document will guide you through compiling a .NET Core [MonoGame](http://www.monogame.net) game with CoreRT.

_Please ensure that [pre-requisites](../prerequisites.md) are installed._

## Create .NET Core MonoGame project
Open a new shell/command prompt window and run the following commands.
```bash
> dotnet new --install MonoGame.Template.CSharp
> dotnet new mgdesktopgl -o MyGame
> cd MyGame
```

This will install .NET Core MonoGame template and create empty game project. .NET Core MonoGame port lives at https://github.com/cra0zy/MonoGame/tree/core currently. Thank you @cra0zy for the great work! 

Verify that the empty game builds and runs. You should see blue window:

```bash
> dotnet run
```

MonoGame tools require [Mono](http://www.mono-project.com/download/) on non-Windows platforms. On Windows, MonoGame tools depend on [Visual Studio 2012 Visual C++ redistributable](https://www.microsoft.com/en-us/download/details.aspx?id=30679).

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

## Try MonoGame sample game

Clone MonoGame samples from github:

```bash
> git clone https://github.com/MonoGame/MonoGame.Samples
```

MonoGame samples include project files for number of targets, but not for .NET Core yet. One has to create the project using above steps and transplant links to sample sources and assets to it. This directory contains .NET Core project for Platformer2D and NeonShooter samples that assume MonoGame.Samples is cloned under it. Build it and start playing!

```bash
> dotnet publish -r win-x64 -c release Platformer2D.csproj
> bin\x64\Release\netcoreapp2.1\publish\Platformer2D.exe
```

The NeonShooter sample works on Windows-only due to https://github.com/MonoGame/MonoGame/issues/3270.

## Using reflection 
Runtime directives are XML configuration files, which specify which otherwise unreachable elements of your program are available for reflection. They are used at compile-time to enable AOT compilation in applications at runtime. The runtime directives are reference in the project via RdXmlFile item:

```xml
<ItemGroup>
  <RdXmlFile Include="rd.xml" />
</ItemGroup>
```

MonoGame serialization engine uses reflection to create types representing the game assets that needs to mentioned in the [rd.xml](rd.xml) file. If you see MissingMetadataException thrown during game startup, add the missing types to the rd.xml file.

Feel free to modify the sample application and experiment. However, keep in mind some functionality might not yet be supported in CoreRT. Let us know on the [Issues page](https://github.com/dotnet/corert/issues/).
