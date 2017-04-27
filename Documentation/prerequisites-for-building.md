The following pre-requisites need to be installed for building the repo:

# Windows (Windows 7+)

1. Install [Visual Studio 2017](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx), including Visual C++ support. Visual Studio 2015 also works.
2. Install [CMake](http://www.cmake.org/download/) 3.8.0 or later. Make sure you add it to the PATH in the setup wizard.
3. (Windows 7 only) Install PowerShell 3.0. It's part of [Windows Management Framework 3.0](http://go.microsoft.com/fwlink/?LinkID=240290). Windows 8 or later comes with the right version inbox.

PowerShell also needs to be available from the PATH environment variable (it's the default). Typically it should be %SYSTEMROOT%\System32\WindowsPowerShell\v1.0\.

# Ubuntu (14.04)

Install basic dependency packages:

First add a new package source to be able to install clang-3.9:
```sh
echo "deb http://llvm.org/apt/trusty/ llvm-toolchain-trusty-3.9 main" | sudo tee /etc/apt/sources.list.d/llvm.list
wget -O - http://llvm.org/apt/llvm-snapshot.gpg.key | sudo apt-key add -
sudo apt-get update
```

```sh
sudo apt-get install cmake clang-3.9 libicu52 libunwind8
```

# Mac OSX (10.11.5+)

1. Install [Command Line Tools for XCode 8](https://developer.apple.com/xcode/download/) or higher. 
2. Install [CMake](https://cmake.org/download/). Launch `/Applications/CMake.app/Contents/MacOS/CMake` GUI. Goto "OSX App Menu -> Tools -> Install For Command Line Use" and follow the steps. CMake < 3.6 has [a bug](https://cmake.org/Bug/view.php?id=16064) that can make `--install` fail. Do `sudo mkdir /usr/local/bin` to work around.
3. Install OpenSSL. Build tools have a dependency on OpenSSL. You can use [Homebrew](http://brew.sh/) to install it: with Homebrew installed, run `brew install openssl` followed by `ln -s /usr/local/opt/openssl/lib/libcrypto.1.0.0.dylib /usr/local/lib/`, followed by `ln -s /usr/local/opt/openssl/lib/libssl.1.0.0.dylib /usr/local/lib/`.
4. Install ICU (International Components for Unicode). It can be obtained via [Homebrew](http://brew.sh/): run `brew install icu4c` followed by `brew link --force icu4c` 
