# Intro to CoreRT Repo

CoreRT is A .NET Core runtime optimized for AoT (Ahead of Time Compilation) scenarios.  It is a minimal runtime to allow for things like Garbage Collection for .NET Native.

This repo also contains the .NET Native toolchain, which allows for compiling .NET Core apps into native binaries.

To get an idea for all the pieces of this system, see [the high-level engineering plan](High-level-engineering-plan.md).

## Relevant Docs for Contributing

- [Prerequisites](Pre-requisites-for-Building.md)
- [How to build and run from the Command Line](How-to-build-and-run-ILCompiler-in-Console-Shell-prompt.md)
- [How to build and run from Visual Studio](How-to-build-and-run-ILCompiler-in-Visual-Studio-2015.md)
- [How to run tests](How-to-Run-Tests.md)
- [NuGet Dependencies Required](Nuget-Dependencies-for-the-Toolchain.md)
