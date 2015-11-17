# Intro to CoreRT Repo

CoreRT is A .NET Core runtime optimized for AoT (Ahead of Time Compilation) scenarios.  It is a minimal runtime to allow for things like Garbage Collection for .NET Native.

This repo also contains the .NET Native toolchain, which allows for compiling .NET Core apps into native binaries.

This project is in early stages of its development.  [The high-level engineering plan](High-level-engineering-plan.md) lists major parts that needs to come together for it to become a complete .NET Core runtime.

## Contributing

*We follow the same [Contribution Guidelines](https://github.com/dotnet/corefx/blob/master/Documentation/project-docs/contributing.md) as CoreFX.*

- [Prerequisites](prerequisites-for-building.md)
- [How to build and run from the Command Line](how-to-build-and-run-ilcompiler-in-console-shell-prompt.md)
- [How to build and run from Visual Studio](how-to-build-and-run-ilcompiler-in-visual-studio-2015.md)
- [How to run tests](how-to-run-tests.md)
- [NuGet Dependencies Required](nuget-dependencies-for-the-toolchain.md)
