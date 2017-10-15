Intro to .NET Native and CoreRT
===============================

Native compilation is a great scenario addition to .NET Core apps on Windows, OS X and Linux. We've seen significant startup and throughput benefits of native compilation for Windows UWP apps, using .NET Native. Today, many native apps and tools benefit from being compiled by a C++ compiler, and not as much by being written in C++. .NET Native brings much of the performance and all of the deployment benefits of native compilation, while retaining your ability to write in your favorite .NET programming language.

Architecture
============

[.NET Native](https://msdn.microsoft.com/library/dn584397.aspx) is a native  toolchain that compiles [CIL byte code](https://en.wikipedia.org/wiki/Common_Intermediate_Language) to machine code (e.g. X64 instructions). By default, .NET Native (for .NET Core, as opposed to UWP) uses RyuJIT as an ahead-of-time (AOT) compiler, the same one that CoreCLR uses as a just-in-time (JIT) compiler. It can also be used with other compilers, such as [LLILC](https://github.com/dotnet/llilc), UTC for UWP apps and [IL to CPP](https://github.com/dotnet/corert/tree/master/src/ILCompiler.CppCodeGen/src/CppCodeGen) (an IL to textual C++ compiler we have built as a reference prototype).

[CoreRT](https://github.com/dotnet/corert) is the .NET Core runtime that is optimized for AOT scenarios, which .NET Native targets. This is a refactored and layered runtime. The base is a small native execution engine that provides services such as garbage collection(GC). This is the same GC used in CoreCLR. Many other parts of the traditional .NET runtime, such as the [type system](https://github.com/dotnet/corert/tree/master/src/Common/src/TypeSystem), are implemented in C#. We've always wanted to implement runtime functionality in C#. We now have the infrastructure to do that. In addition, library implementations that were built deep into CoreCLR, have also been cleanly refactored and implemented as C# libraries. 

Experience
==========

.NET Native offers great benefits that are critical for many apps. 

- The native compiler generates a *SINGLE FILE*, including the app, managed dependencies and CoreRT.
- Native compiled apps startup faster since they execute already compiled code. They don't need to generate machine code at runtime nor load a JIT compiler.
- Native compiled apps can use an optimizing compiler, resulting in faster throughput from higher quality code (C++ compiler optimizations). Both the LLILLC and IL to CPP compilers rely on optimizing compilers.

These benefits open up some new scenarios for .NET developers

- Copy a single file executable from one machine and run on another (of the same kind) without installing a .NET runtime.
- Create and run a docker image that contains a single file executable (e.g. one file in addition to Ubuntu 14.04).

Roadmap
=======

To start, we are targeting native executables (AKA "console apps"). Over time, we'll extend that to include ASP.NET Core apps. You can continue to use CoreCLR for your .NET Core apps. It remains a great option if native compilation isn't critical for your needs. CoreCLR will also provide a superior debugging experience until we add debugging support to CoreRT.
