## Building Native Libraries ##

CoreRT can also be used to build native libraries that can be consumed by other programming languages. It can build static libraries that can be linked at compile time or shared libraries that are required at runtime. To build a native library you must first modify the `OutputType` and `TargetFramework` in the above `csproj`:

```
<Project>
  <Import Project="$(MSBuildSDKsPath)\Microsoft.NET.Sdk\Sdk\Sdk.props" />

  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>

  <Import Project="$(MSBuildSDKsPath)\Microsoft.NET.Sdk\Sdk\Sdk.targets" />
  <Import Project="$(IlcPath)\build\Microsoft.NETCore.Native.targets" />
</Project>
```

Building static libraries:

``` 
    dotnet publish /p:NativeLib=Static -r win-x64|linux-x64|osx-x64
```

The above command will drop a static library (Windows `.lib`, OSX/Linux `.a`) in `./bin/x64/[configuration]/netstandard2.0/publish/` folder and will have the same name as the folder in which your source file is present.

Building shared libraries:

``` 
    dotnet publish /p:NativeLib=Shared -r win-x64|linux-x64|osx-x64
```

The above command will drop a shared library (Windows `.dll`, OSX `.dylib`, Linux `.so`) in `./bin/x64/[configuration]/netstandard2.0/publish/` folder and will have the same name as the folder in which your source file is present. Building shared libraries on Linux is currently non-functional, see [#4988](https://github.com/dotnet/corert/issues/4988).

Exporting methods:

For a C# method in the native library to be consumable by external programs, it has to be explicitly exported using the `[NativeCallable]` attribute. First define the `NativeCallable` class in your project, see [here](https://github.com/dotnet/corert/blob/master/tests/src/Simple/SharedLibrary/NativeCallable.cs). Next, apply the attribute to the method, specifying the `EntryPoint` and `CallingConvention` properties:

```csharp
[NativeCallable(EntryPoint = "add", CallingConvention = CallingConvention.StdCall)]
public static int Add(int a, int b)
{
    return a + b;
}
```

After the native library library is built, the above C# `Add` method will be exported as a native `add` function to consumers of the library. Here are some limitations to consider when deciding what managed method to export:

* Exported methods have to be static.
* Exported methods can only naturally accept or return primitives or value types (i.e structs), they have to marshal all reference type arguments.
* Exported methods cannot be called from regular managed C# code, an exception will be thrown.
* Exported methods cannot use regular C# exception handling, they should return error codes instead.
