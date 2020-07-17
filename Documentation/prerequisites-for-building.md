The following pre-requisites need to be installed for building the repo:

# Windows (Windows 7+)

1. Install [Visual Studio 2019](https://visualstudio.microsoft.com/vs/community/), including Visual C++ support.
 - For an existing Visual Studio 2019 installation, `buildscripts/install-reqs-vs2019.cmd` is provided.
2. Install [CMake](http://www.cmake.org/download/) 3.14.0 or later. Make sure you add it to the PATH in the setup wizard.
3. (Windows 7 only) Install PowerShell 3.0. It's part of [Windows Management Framework 3.0](http://go.microsoft.com/fwlink/?LinkID=240290). Windows 8 or later comes with the right version inbox.

PowerShell also needs to be available from the PATH environment variable (it's the default). Typically it should be %SYSTEMROOT%\System32\WindowsPowerShell\v1.0\.

# Ubuntu (16.04+)

```sh
sudo apt-get install llvm cmake clang libicu-dev uuid-dev libcurl4-openssl-dev zlib1g-dev libkrb5-dev libtinfo5
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

Then follow the Ubuntu 16.04 instructions above.

# clang not found on Linux

If you encounter this error, CoreRT could not find the clang executable
```
error : Platform linker ('clang') not found. Try installing clang package for your platform to resolve the problem.
```

You can override the default name by setting an environment variable. This is useful when clang executable on your platform has version specific suffix.

```
export CppCompilerAndLinker=clang-3.9
```

This works for building CoreCR itself as well as building with CoreRT.
When filing bugs, please make sure to mention the clang version you are using, if you override this.

# libObjWriter.so not found on Linux

```
EXEC : error : Unable to load shared library 'objwriter' or one of its dependencies. In order to help diagnose loading problems, consider setting the LD_DEBUG environment variable: libobjwriter: cannot open shared object file: No such file or directory 
```

This is the default error message when a `[DllImport]` could not be loaded. CoreRT nuget packages distribute this file, but it might be failing to load dependencies.
Make sure to install all dependencies. If the error persists, use ldd to find out if you lack any dependencies. 

```
ldd /home/<username>/.nuget/packages/runtime.linux-x64.microsoft.dotnet.ilcompiler/<nuget_package_version>/tools/libobjwriter.so
    linux-vdso.so.1 (0x00007ffe9bbea000)
    librt.so.1 => /usr/lib/librt.so.1 (0x00007f8f52142000)
    libdl.so.2 => /usr/lib/libdl.so.2 (0x00007f8f5213d000)
    libtinfo.so.5 => not found
    libpthread.so.0 => /usr/lib/libpthread.so.0 (0x00007f8f5211c000)
    libz.so.1 => /usr/lib/libz.so.1 (0x00007f8f51f05000)
    libm.so.6 => /usr/lib/libm.so.6 (0x00007f8f51d80000)
    libstdc++.so.6 => /usr/lib/libstdc++.so.6 (0x00007f8f51bef000)
    libgcc_s.so.1 => /usr/lib/libgcc_s.so.1 (0x00007f8f51bd5000)
    libc.so.6 => /usr/lib/libc.so.6 (0x00007f8f51a11000)
    /usr/lib64/ld-linux-x86-64.so.2 (0x00007f8f53358000)
```

In this Arch Linux example, libtinfo.so.5 is missing. Its part of ncurses5 but AUR has a compatibility package here: 
https://aur.archlinux.org/packages/ncurses5-compat-libs/
After installing it should work fine
