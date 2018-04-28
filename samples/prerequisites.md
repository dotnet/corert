If you're new to .NET Core make sure to visit the [official starting page](http://dotnet.github.io). It will guide you through installing pre-requisites and building your first app.
If you're already familiar with .NET Core make sure you've [downloaded and installed the .NET Core 2 SDK](https://www.microsoft.com/net/download/core).

The following pre-requisites need to be installed for building .NET Core projects with CoreRT:

# Windows

* Install [Visual Studio 2017](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx), including Visual C++ support.

# Ubuntu (14.04+)

* Install clang and developer packages for libraries that .NET Core depends on.

```sh
sudo apt-get install libcurl4-openssl-dev zlib1g-dev libkrb5-dev
```

# macOS (10.12+)

* Install [Command Line Tools for XCode 8](https://developer.apple.com/xcode/download/) or higher.
