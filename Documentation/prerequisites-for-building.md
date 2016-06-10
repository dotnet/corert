The following pre-requisites need to be installed for building the repo:

# Windows (Windows 7+)

1. Install [Visual Studio 2015](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx), including Visual C++ support.
2. Install [CMake](http://www.cmake.org/download/). We use CMake 3.3.2 but any later version should work.
3. (Windows 7 only) Install PowerShell 3.0. It's part of [Windows Management Framework 3.0](http://go.microsoft.com/fwlink/?LinkID=240290). Windows 8 or later comes with the right version inbox.

PowerShell also needs to be available from the PATH environment variable (it's the default). Typically it should be %SYSTEMROOT%\System32\WindowsPowerShell\v1.0\.

# Ubuntu (14.04)

Install basic dependency packages:

```
sudo apt-get install cmake llvm-3.5 clang-3.5 lldb-3.6 lldb-3.6-dev libunwind8 libunwind8-dev liblttng-ust liblttng-ust-dev uuid uuid-dev
```

# Mac OSX (10.10+)

1. Install [Command Line Tools for XCode 6.3.1](https://developer.apple.com/xcode/download/) or higher. 
2. Install [CMake](https://cmake.org/download/). Launch `/Applications/CMake.app/Contents/MacOS/CMake` GUI. Goto "OSX App Menu -> Tools -> Install For Command Line Use" and follow the steps. CMake < 3.6 has [a bug](https://cmake.org/Bug/view.php?id=16064) that can make `--install` fail. Do `sudo mkdir /usr/local/bin` to work around.
3. Install OpenSSL. Build tools have a dependency on OpenSSL. You can use [Homebrew](http://brew.sh/) to install it: with Homebrew installed, run `brew install openssl` followed by `brew link --force openssl`.
