The following pre-requisites need to be installed for building the repo:

# Windows (Windows 7+)

1. Install [Visual Studio 2017](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx), including Visual C++ support.
2. Install [CMake](http://www.cmake.org/download/) 3.8.0 or later. Make sure you add it to the PATH in the setup wizard.
3. (Windows 7 only) Install PowerShell 3.0. It's part of [Windows Management Framework 3.0](http://go.microsoft.com/fwlink/?LinkID=240290). Windows 8 or later comes with the right version inbox.

PowerShell also needs to be available from the PATH environment variable (it's the default). Typically it should be %SYSTEMROOT%\System32\WindowsPowerShell\v1.0\.

# Ubuntu (14.04+)

Install basic dependency packages:

First add a new package source to be able to install clang-3.9:
```sh
echo "deb http://llvm.org/apt/trusty/ llvm-toolchain-trusty-3.9 main" | sudo tee /etc/apt/sources.list.d/llvm.list
wget -O - http://llvm.org/apt/llvm-snapshot.gpg.key | sudo apt-key add -
sudo apt-get update
```

```sh
sudo apt-get install cmake clang-3.9 libicu52 libunwind8 uuid-dev libcurl4-openssl-dev zlib1g-dev libkrb5-dev
```

# macOS (10.12+)

1. Install [Command Line Tools for XCode 8](https://developer.apple.com/xcode/download/) or higher. 
2. Install [CMake](https://cmake.org/download/) 3.8.0 or later. Launch `/Applications/CMake.app/Contents/MacOS/CMake` GUI. Goto "OSX App Menu -> Tools -> Install For Command Line Use" and follow the steps.

# openSUSE Leap 42.3

First install llvm-3.9. This is a bit cumbersome because the LLVM that comes from the `zypper` feeds is too old.

```sh
wget http://releases.llvm.org/3.9.0/clang+llvm-3.9.0-x86_64-opensuse13.2.tar.xz
tar xf clang+llvm-3.9.0-x86_64-opensuse13.2.tar.xz
cd clang+llvm-3.9.0-x86_64-opensuse13.2
sudo cp -R * /usr/local/
```

Next install the rest of the dependencies:

```sh
sudo zypper install cmake libuuid-devel icu libcurl-devel zlib-devel
```

# Bash on Ubuntu on Windows (Windows 10 Creators Update or later)

Make sure you run with Ubuntu 16.04 Xenial userland (this is the default after Windows 10 Creators Update, but if you enabled the "Bash on Ubuntu on Windows" feature before the Creators Update, you need to [upgrade manually](https://blogs.msdn.microsoft.com/commandline/2017/04/11/windows-10-creators-update-whats-new-in-bashwsl-windows-console/)). Running `lsb_release -a` will give you the version.

Install basic dependency packages:

First add a new package source to be able to install clang-3.9:
```sh
echo "deb http://llvm.org/apt/xenial/ llvm-toolchain-xenial-3.9 main" | sudo tee /etc/apt/sources.list.d/llvm.list
wget -O - http://llvm.org/apt/llvm-snapshot.gpg.key | sudo apt-key add -
sudo apt-get update
```

```sh
sudo apt-get install cmake clang-3.9 libunwind8 uuid-dev libcurl4-openssl-dev zlib1g-dev libkrb5-dev
```
