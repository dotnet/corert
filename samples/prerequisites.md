If you're new to .NET Core make sure to visit the [official starting page](http://dotnet.github.io). It will guide you through installing pre-requisites and building your first app.
If you're already familiar with .NET Core make sure you've [downloaded and installed the .NET Core 3.1 SDK](https://www.microsoft.com/net/download/core).

The following pre-requisites need to be installed for building .NET Core projects with CoreRT:

# Windows

* Install [Visual Studio 2019](https://visualstudio.microsoft.com/vs/community/), including Visual C++ support.

# Fedora (31+)

Tested on Fedora 31, will most likely work on lower versions, too.

```sh
sudo dnf install clang zlib-devel krb5-libs krb5-devel ncurses-compat-libs
```

# Ubuntu (16.04+)

* Install clang and developer packages for libraries that .NET Core depends on.

```sh
sudo apt-get install clang zlib1g-dev libkrb5-dev libtinfo5
```

# macOS (10.12+)

* Install [Command Line Tools for XCode 8](https://developer.apple.com/xcode/download/) or higher.
