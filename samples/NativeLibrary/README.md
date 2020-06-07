# Building Native Libraries with CoreRT

This document will guide you through building native libraries that can be consumed by other programming languages with CoreRT. CoreRT can build static libraries that can be linked at compile time or shared libraries that are required at runtime.

## Create .NET Core Class Library project with CoreRT support

Create a .NET Core class library project using `dotnet new classlib -o NativeLibrary` and follow the [Hello world](../HelloWorld/README.md) sample instruction to add CoreRT support to it.

## Building static libraries

```bash
> dotnet publish /p:NativeLib=Static -r <RID> -c <Configuration>
```

where `<Configuration>` is your project configuration (such as Debug or Release) and `<RID>` is the runtime identifier (one of win-x64, linux-x64, osx-x64). For example, if you want to publish a release configuration of your library for a 64-bit version of Windows the command would look like:

```bash
> dotnet publish /p:NativeLib=Static -r win-x64 -c release
```

The above command will drop a static library (Windows `.lib`, OSX/Linux `.a`) in `./bin/[configuration]/netstandard2.0/[RID]/publish/` folder and will have the same name as the folder in which your source file is present.

## Building shared libraries

```bash
> dotnet publish /p:NativeLib=Shared -r <RID> -c <Configuration>
```

The above command will drop a shared library (Windows `.dll`, OSX `.dylib`, Linux `.so`) in `./bin/[configuration]/netstandard2.0/[RID]/publish/` folder and will have the same name as the folder in which your source file is present.

### Loading shared libraries from C and importing methods

For reference, you can read the file LoadLibrary.c.
The first thing you'll have to do in order to have a proper "loader" that loads your shared library is to add these directives

```c
#ifdef _WIN32
#include "windows.h"
#define symLoad GetProcAddress GetProcAddress
#else
#include "dlfcn.h"
#define symLoad dlsym
#endif
```

After these, in order to load the 'handle' of the shared library

```c
#ifdef _WIN32
HINSTANCE handle = LoadLibrary(path);
#else
void *handle = dlopen(path, RTLD_LAZY);
#endif
```

the variable path is the string that holds the path to the .so/.dll file.
From now on, the handle variable will "contain" a pointer to your shared library.
Now we'll have to define what type does the function we want to call will return

```c
typedef  int (*myFunc)();
```

For example here, we'll refer to the C# function underneath, which returns the sum of two integers.
Now we'll import from handle , that as we said points to our shared library , the function we want to call

```c
myFunc MyImport =  symLoad(handle, funcName);
```

where funcName is a string that contains the name of the entrypoint value defined in the UnmanagedCallersOnly field.
The last thing to do is to actually call the method we have imported.

```c
int result =  MyImport(5,3);
```

## Exporting methods

For a C# method in the native library to be consumable by external programs, it has to be explicitly exported using the `[UnmanagedCallersOnly]` attribute. First define the `System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute` in your project, see [here](UnmanagedCallersOnly.cs). The local definition of the `UnmanagedCallersOnlyAttribute` is a temporary workaround that will go away once the attribute is added to the official .NET Core public surface.

Next, apply the attribute to the method, specifying the `EntryPoint`:

```csharp
[UnmanagedCallersOnly(EntryPoint = "add")]
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

The sample [source code](Class1.cs) demonstrates common techniques used to stay within these limitations.

## References

Real-world example of using CoreRT and Rust: https://medium.com/@chyyran/calling-c-natively-from-rust-1f92c506289d