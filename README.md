# .NET Core Runtime (CoreRT)

### This project is superseded by [NativeAOT experiment in dotnet/runtimelab repo]( https://github.com/dotnet/runtimelab/tree/feature/NativeAOT).

This repo contains the .NET Core runtime optimized for ahead of time compilation. The CoreRT compiler can compile a managed .NET Core application into a native (architecture specific) single-file executable that is easy to deploy. It can also produce standalone dynamic or static libraries that can be consumed by applications written in other programming languages. To learn more about CoreRT, see the [intro document](Documentation/intro-to-corert.md).

## Try Our Samples

If you would like to give CoreRT a try, we publish daily snapshots of CoreRT to a NuGet feed. Using CoreRT is as simple as adding a new package reference to your .NET Core project and publishing it. Check out one of our samples: a "[Hello World](samples/HelloWorld)" console app, a simple [ASP.NET Core](samples/WebApi/) app, a [MonoGame](samples/MonoGame/) game or a [native library](samples/NativeLibrary). The `README.md` file in each sample's directory will guide you through the process step by step.

## Platforms

- Windows, MacOS and Linux x64 w/ RyuJIT codegen is able to compile many complex apps.
   - [ASP.NET Core](samples/WebApi/) sample
   - [MonoGame](samples/MonoGame/) sample
   - Avalonia [sample](https://github.com/teobugslayer/AvaloniaCoreRTDemo) and [demo video](https://www.youtube.com/watch?v=iaC67CUmEXs)
   - [ADO.NET](https://github.com/ifew/corert-db) sample
   - [EntityFrameworkCore.Sqlite](https://github.com/rubin55/dot-hello) sample 
   - Unsupported features: [Dynamic loading](https://github.com/dotnet/corert/issues/6949) (e.g. `Assembly.LoadFile`), [dynamic code generation](https://github.com/dotnet/corert/issues/5011) (e.g. `System.Reflection.Emit`), [Windows-specific interop](https://github.com/dotnet/corert/issues/4219) (e.g. COM, WinRT)
- Linux ARM w/ RyuJIT codegen: ElmSharp Hello Tizen application ([detailed status](https://github.com/dotnet/corert/issues/4856))
- CppCodeGen (targets all platforms that support C++): Simple C# programs. The big missing features are [garbage collection](https://github.com/dotnet/corert/issues/2033) and [exception handling](https://github.com/dotnet/corert/issues/910).
- WebAssembly: Early prototype that compiles and runs very trivial programs only. Many features are [not yet implemented](https://github.com/dotnet/corert/issues?q=is%3Aissue+is%3Aopen+label%3Aarch-wasm).

## How to Engage, Contribute and Provide Feedback
Some of the best ways to contribute are to try things out, file bugs, and join in design conversations.

Looking for something to work on? The [_up for grabs_](https://github.com/dotnet/corert/labels/up-for-grabs) issues are a great place to start. Take a look at our [documentation](Documentation) to find out about the architecture and learn how to build and test the repo.

This project follows the [.NET Core Contribution Guidelines](https://github.com/dotnet/coreclr/blob/master/Documentation/project-docs/contributing.md).

[![Join the chat at https://gitter.im/dotnet/corert](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/dotnet/corert?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

### .NET Native for UWP Support

Use https://developercommunity.visualstudio.com/ to report problems and suggestions related to [.NET Native for UWP](https://docs.microsoft.com/en-us/dotnet/framework/net-native/).

### Reporting security issues and security bugs

Security issues and bugs should be reported privately, via email, to the
Microsoft Security Response Center (MSRC) <secure@microsoft.com>. You should
receive a response within 24 hours. If for some reason you do not, please follow
up via email to ensure we received your original message. Further information,
including the MSRC PGP key, can be found in the
[Security TechCenter](https://technet.microsoft.com/en-us/security/ff852094.aspx).

## License
The CoreRT Repo is licensed under the [MIT license](https://github.com/dotnet/corert/blob/master/LICENSE.TXT).

## .NET Foundation
CoreRT is a [.NET Foundation](http://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## Related Projects
There are many .NET related projects on GitHub.
- The [.NET home repo](https://github.com/Microsoft/dotnet) links to 100s of .NET projects, from Microsoft and the community.
- The [ASP.NET Core repo](https://github.com/aspnet/AspNetCore) is the best place to start learning about [ASP.NET Core](http://www.asp.net).
