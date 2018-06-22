# Building a WebAPI app with CoreRT

CoreRT is an AOT-optimized .NET Core runtime. This document will guide you through compiling a .NET Core Web API application with CoreRT. 

_Please ensure that [pre-requisites](../prerequisites.md) are installed._

## Create your app 
Open a new shell/command prompt window and run the following commands.
```bash
> dotnet new webapi -o myApp
> cd myApp
```

## Add CoreRT to your project
Using CoreRT to compile your application is done via the ILCompiler NuGet package, which is [published to MyGet with the CoreRT daily builds](https://dotnet.myget.org/feed/dotnet-core/package/nuget/Microsoft.DotNet.ILCompiler).
For the compiler to work, it first needs to be added to your project.

In your shell/command prompt navigate to the root directory of your project and run the command:

```bash
> dotnet new nuget 
```

This will add a nuget.config file to your application. Open the file and in the ``<packageSources> `` element under ``<clear/>`` add the following:

```xml
<add key="dotnet-core" value="https://dotnet.myget.org/F/dotnet-core/api/v3/index.json" />
<add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
```

Once you've added the package source, add a reference to the compiler by running the following command:

```bash
> dotnet add package Microsoft.DotNet.ILCompiler -v 1.0.0-alpha-* 
```

## Add Core MVC services
With the package successfully added to your project, your project's default registered MVC services must be modified.

The default template's `AddMvc()` call registers a large set of middleware services by default, even if they are not needed by your application. This is not AOT-compilation friendly because it leads to large binaries and creates the risk of adding unsupported features.

Open the file called `Startup.cs` and in the `ConfigureServices()` method and modify the line:

```csharp
services.AddMvc();
```

to

```csharp
var applicationPartManager = new ApplicationPartManager();
applicationPartManager.ApplicationParts.Add(new AssemblyPart(typeof(Startup).Assembly));
services.Add(new ServiceDescriptor(typeof(ApplicationPartManager), applicationPartManager));

services.AddMvcCore().AddJsonFormatters();
```

Replacing `AddMvc()` with `AddMvcCore()` adds only the basic MVC functionality. It's followed by explicit registration of services, which are required by the application - in this case the `JsonFormatter`. For more details see [Fabian Gosebrink's blog post comparing AddMvc and AddMvcCore](https://dzone.com/articles/the-difference-between-addmvc-and-addmvccore).

## Using reflection 
Runtime directives are XML configuration files, which specify which elements of your program are available for reflection. They are used at compile-time to enable AOT compilation in applications at runtime. 

In this sample a basic rd.xml file has been added for a simple Web API application under the root project folder. Copy its contents to your application directory and modify the element
```xml
 <Assembly Name="SampleWebApi" Dynamic="Required All" /> 
 ``` 
 to use your app's name.

If your application makes use of reflection, you will need to create a rd.xml file specifying explicitly which assemblies and types should be made available. For example, in  your .NET Core Web API application, reflection is required to determine the correct namespace, from which to load the ``Startup`` type. Both are defined respectively via the `<Assembly>` and `<Type>` attributes. For example, in the case of our specific application:

```xml 
<Assembly Name="SampleWebApi">
  <Type Name="SampleWebApi.Startup" Dynamic="Required All" />
</Assembly>
```

At runtime, if a method or type is not found or cannot be loaded, an exception will be thrown. The exception message will contain information on the missing type reference, which you can then add to the rd.xml of your program.

Once you've created a rd.xml file, navigate to the root directory of your project and open its `.csproj` file and in the first `<ItemGroup>` element add the following:

```xml
<RdXmlFile Include="path_to_rdxml_file\rd.xml" />
```

where path_to_rdxml_file is the location of the file on your disk.

Under the second `<ItemGroup>` remove the line containing a reference to `Microsoft.AspNetCore.All` and substitute it with:

```xml
<PackageReference Include="Microsoft.AspNetCore" Version="2.1.0" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Core" Version="2.1.0" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Formatters.Json" Version="2.1.0" />
```

This substitution removes unnecessary package references added by AspNetCore.All, which will remove them from your application's published files and avoid encountering unsupported features, as described in [the section above](#add-core-mvc-services)

After you've modified your project's `.csproj` file, open your application's controller file (in the default template this should be called `ValuesController.cs`) and substitute the ValuesController class with the following: 

```csharp 
public class ValuesController
{ 
    [HttpGet("/")]
    public string Hello() => "Hello World!";
    // GET api/values
    [HttpGet("/api/values")]
    public IEnumerable<string> Get()
    {
        return new string[] { "value1", "value2" };
    }
    // GET api/values/5
    [HttpGet("/api/values/{id}")]
    public string Get(int id)
    {
        return "Your value is " + id;
    }
}
```

(note the removed inheritance and [Route] directive). Also note that URL request paths are explicitly defined on each method. 


## Restore and Publish your app

Once the package has been successfully added it's time to compile and publish your app! In the shell/command prompt window, run the following command:

```bash
> dotnet publish -r <RID> -c <Configuration>
```

where `<Configuration>` is your project configuration (such as Debug or Release) and `<RID>` is the runtime identifier (one of win-x64, linux-x64, osx-x64). For example, if you want to publish a release configuration of your app for a 64-bit version of Windows the command would look like:

```bash 
> dotnet publish -r win-x64 -c release
```

Once completed, you can find the native executable in the root folder of your project under `/bin/x64/<Configuration>/netcoreapp2.1/publish/`

## Try it out!

If you are running macOS, make sure you have [libuv](https://github.com/libuv/libuv) installed, as ASP.NET is built on top of libuv. You can use [homebrew](https://brew.sh/) to get it (`brew install libuv`).

Navigate to `/bin/x64/<Configuration>/netcoreapp2.1/publish/` in your project folder and run the produced executable. It should display "Now listening on: http://localhost:XXXX" with XXXX being a port on your machine. Open your browser and navigate to that URL. You should see "Hello World!" displayed in your browser.

Feel free to modify the sample application and experiment. However, keep in mind some functionality might not yet be supported in CoreRT. Let us know on the [Issues page](https://github.com/dotnet/corert/issues/).
