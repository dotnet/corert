# .NET Core Runtime (CoreRT)
This repo contains the .NET Core runtime optimized for AOT compilation

## Platform Support

This is a work in progress. The current state of platform support:
- Windows, MacOS and Linux x64 w/ RyuJIT codegen: Simple apps. Check our [ASP.NET Core](samples/WebApi/) and [MonoGame](samples/MonoGame/) samples.
- Linux ARM w/ RyuJIT codegen: ElmSharp Hello Tizen application ([detailed status](https://github.com/dotnet/corert/issues/4856))
- CppCodeGen (targets all platforms that support C++): Simple C# programs. The big missing features are [reflection](https://github.com/dotnet/corert/issues/2035), [garbage collection](https://github.com/dotnet/corert/issues/2033) and [exception handling](https://github.com/dotnet/corert/issues/910).
- WebAssembly: Early prototype that compiles and runs very trivial programs only. Many features are [not yet implemented](https://github.com/dotnet/corert/issues?q=is%3Aissue+is%3Aopen+label%3Aarch-wasm).

## How to Engage, Contribute and Provide Feedback
Some of the best ways to contribute are to try things out, file bugs, and join in design conversations.

Looking for something to work on? The [_up for grabs_](https://github.com/dotnet/corert/labels/up-for-grabs) issues are a great place to start or take a look at our [documentation](Documentation).

This project follows the [.NET Core Contribution Guidelines](https://github.com/dotnet/coreclr/blob/master/Documentation/project-docs/contributing.md).

[![Join the chat at https://gitter.im/dotnet/corert](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/dotnet/corert?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

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
- The [ASP.NET home repo](https://github.com/aspnet/home) is the best place to start learning about [ASP.NET Core](http://www.asp.net).

## Build Status

|         |Ubuntu 14.04 |Windows |Mac OS X |
|---------|:------:|:------:|:------:|
|**Debug**|[![Build status](https://ci.dot.net/job/dotnet_corert/job/master/job/debug_ubuntu/badge/icon)](https://ci.dot.net/job/dotnet_corert/job/master/job/debug_ubuntu/)|[![Build status](https://ci.dot.net/job/dotnet_corert/job/master/job/debug_windows_nt/badge/icon)](https://ci.dot.net/job/dotnet_corert/job/master/job/debug_windows_nt/)|[![Build Status](https://ci.dot.net/job/dotnet_corert/job/master/job/debug_osx10.12/badge/icon)](https://ci.dot.net/job/dotnet_corert/job/master/job/debug_osx10.12/)|
|**Release**|[![Build status](https://ci.dot.net/job/dotnet_corert/job/master/job/release_ubuntu/badge/icon)](https://ci.dot.net/job/dotnet_corert/job/master/job/release_ubuntu/)|[![Build status](https://ci.dot.net/job/dotnet_corert/job/master/job/release_windows_nt/badge/icon)](https://ci.dot.net/job/dotnet_corert/job/master/job/release_windows_nt/)|[![Build Status](https://ci.dot.net/job/dotnet_corert/job/master/job/release_osx10.12/badge/icon)](https://ci.dot.net/job/dotnet_corert/job/master/job/release_osx10.12/)|
