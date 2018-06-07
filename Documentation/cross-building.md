Cross Compilation for ARM on Linux
==================================

Through cross compilation, on Linux it is possible to build CoreRT for arm or arm64.

Requirements
------------

You need a Debian based host and the following packages needs to be installed:

    $ sudo apt-get install qemu qemu-user-static binfmt-support debootstrap

In addition, to cross compile CoreCLR the binutils for the target are required. So for arm you need:

    $ sudo apt-get install binutils-arm-linux-gnueabihf

and conversely for arm64:

    $ sudo apt-get install binutils-aarch64-linux-gnu


Generating the rootfs
---------------------
The `cross\build-rootfs.sh` script can be used to download the files needed for cross compilation. It will generate an Ubuntu 14.04 rootfs as this is what CoreRT targets.

    Usage: build-rootfs.sh [BuildArch]
    BuildArch can be: arm, arm64

The `build-rootfs.sh` script must be run as root as it has to make some symlinks to the system, it will by default generate the rootfs in `cross\rootfs\<BuildArch>` however this can be changed by setting the `ROOTFS_DIR` environment variable.

For example, to generate an arm rootfs:

    $ sudo ./cross/build-rootfs.sh arm

and if you wanted to generate the rootfs elsewhere:

    $ sudo ROOTFS_DIR=~/coreclr-cross/arm ./build-rootfs.sh arm

Cross compiling CoreCLR
-----------------------
Once the rootfs has been generated, it will be possible to cross compile CoreRT. If `ROOTFS_DIR` was set when generating the rootfs, then it must also be set when running `build.sh`.

So, without `ROOTFS_DIR`:

    $ ./build.sh arm debug verbose clean cross

And with:

    $ ROOTFS_DIR=~/coreclr-cross/arm ./build.sh arm debug verbose clean cross

As usual the resulting binaries will be found in `bin/Product/BuildOS.BuildArch.BuildType/`

Using CoreRT for cross compiling under arm on x86 host
-----------------------
It's possible to use CoreRT for compiling under arm/armel on x86 host (or on x64 machine using rootfs).
You can build Debug or Release version.
For example Release means: release CoreRT/CoreCLR/CoreFX builds + CoreRT(ILC) release enabled optimizations.
For better components compatibility, if you want to build Debug version, you must compile ALL projects as Debug version.
Otherwise, ALL as Release version.

1. Build CoreCLR for x86
```
sudo ./cross/build-rootfs.sh x86 xenial
./build.sh x86 debug verbose cross
```
2. Build CoreFX
```
sudo ./cross/build-rootfs.sh x86 xenial
./build-native.sh -debug -buildArch=x86 -- verbose cross
./build-managed.sh -debug -verbose
```
3. Build CoreRT for x86 and armel
```
sudo ./cross/build-rootfs.sh armel tizen
sudo ./cross/build-rootfs.sh x86 xenial
./build.sh armel debug verbose cross
./build.sh x86 debug verbose cross crosstarget skiptests
```

4. Copy necessary binaries to working directory in x86 rootfs.
Or in any host directory, if you have 32-bit multiarch-support on your x64 host.
```
# 1) Copy CoreCLR(with CoreRun) part
cp ${CORECLR}/bin/Product/Linux.x86.Debug/* ${WORKING_DIR}

# 2) Copy CoreRT part
cp ${CORERT}/bin/Linux.armel.Debug/tools/ilc.dll ${WORKING_DIR}
cp ${CORERT}/bin/Linux.armel.Debug/tools/ILCompiler.* ${WORKING_DIR}
cp ${CORERT}/bin/Linux.armel.Debug/tools/System.CommandLine.dll ${WORKING_DIR}
cp ${CORERT}/bin/Linux.armel.Debug/tools/Microsoft.DiaSymReader.dll ${WORKING_DIR}
cp -r ${CORERT}/bin/Linux.armel.Debug/framework ${WORKING_DIR}
cp -r ${CORERT}/bin/Linux.armel.Debug/sdk ${WORKING_DIR}

# 3) Copy CoreRT x86 jitinterface with target armel version
cp ${CORERT}/bin/Linux.x86.Debug/tools/armeljitinterface.so ${WORKING_DIR}/jitinterface.so

# 4) Copy CoreFX part
# Copy native architecture dependence libs
cp ${COREFX}/bin/Linux.x86.Debug/native/* ${WORKING_DIR}
# Copy arch independence libs
# This part varies depending on the application, these dependencies for HelloWorld only
NETCORE_PATH=netcoreapp-Linux-Debug-x64
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Runtime.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Runtime.Extensions.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Collections.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Reflection.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Reflection.Metadata.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Collections.Immutable.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Console.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.IO.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Runtime.InteropServices.RuntimeInformation.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Runtime.InteropServices.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Threading.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Diagnostics.Debug.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Linq.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.IO.FileSystem.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.IO.MemoryMappedFiles.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.IO.UnmanagedMemoryStream.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.IO.FileSystem.Primitives.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Runtime.Handles.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Text.Encoding.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Text.Encoding.Extensions.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Reflection.Primitives.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Collections.Concurrent.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Security.Cryptography.Algorithms.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Security.Cryptography.Primitives.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Runtime.CompilerServices.Unsafe.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Globalization.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Private.Xml.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Diagnostics.Tracing.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Buffers.dll ${WORKING_DIR}
cp ${COREFX}/bin/runtime/${NETCORE_PATH}/System.Memory.dll ${WORKING_DIR}
```

5. Rename RyuJIT compiler library
```
# Use cross-compiler library as default for ILC
# libclrjit.so is used by the CoreCLR that compiler runs on,
# and libclrjitilc.so is used for the actual compilation.
cp ${WORKING_DIR}/libarmelnonjit.so ${WORKING_DIR}/libclrjitilc.so
```

6. [Build ObjectWriter library](how-to-build-ObjectWriter.md). You have to compile it on x86 chroot.

7. And to execute use:
```
# Hello.ilc.rsp is in CoreRT armel build. It's necessary to edit the paths on the relatively our working directory.
./corerun ilc.dll --codegenopt "AltJitNgen=*" --verbose @Hello.ilc.rsp

# Any other options to RyuJIT could be passed via --codegenopt argument, e.g.:
# ./corerun ilc.dll --codegenopt "AltJitNgen=*" --codegenopt "NgenDisasm=*" --verbose @Hello.ilc.rsp

# For linking
clang -target arm-linux-gnueabi --sysroot=corert/cross/rootfs/armel -Bcorert/cross/rootfs/armel/usr/lib/gcc/armv7l-tizen-linux-gnueabi/6.2.1 -Lcorert/cross/rootfs/armel/usr/lib/gcc/armv7l-tizen-linux-gnueabi/6.2.1 Hello.o -o Hello corert/bin/Linux.armel.Debug/sdk/libbootstrapper.a corert/bin/Linux.armel.Debug/sdk/libRuntime.a corert/bin/Linux.armel.Debug/sdk/libSystem.Private.CoreLib.Native.a corert/bin/Linux.armel.Debug/framework/System.Native.a corert/bin/Linux.armel.Debug/framework/System.Globalization.Native.a -g -Wl,-rpath,'$ORIGIN' -pthread -lstdc++ -ldl -lm -luuid -lrt -fPIC
```
