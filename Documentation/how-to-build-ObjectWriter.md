# Build ObjectWriter library #

ObjWriter is based on LLVM, so it requires recent CMake and GCC/Clang to build LLVM.
See [LLVM requirements](http://llvm.org/docs/GettingStarted.html#requirements) for more details.

1. Clone LLVM from official LLVM mirror github git repository:

    ```
    git clone -b release_50 https://github.com/llvm-mirror/llvm.git
    ```

2. Copy ObjWriter directory from CoreRT into LLVM tree

    ```
    cp -r CoreRT/src/Native/ObjWriter llvm/tools/
    ```

3. Apply the patch to LLVM:

    ```
    cd llvm
    git apply tools/ObjWriter/llvm.patch
    ```

4. Configure and build LLVM with ObjWriter:

    ```
    mkdir build
    cd build
    cmake ../ -DCMAKE_BUILD_TYPE=Release -DLLVM_OPTIMIZED_TABLEGEN=1 -DHAVE_POSIX_SPAWN=0 -DLLVM_ENABLE_PIC=1 -DLLVM_BUILD_TESTS=0 -DLLVM_ENABLE_DOXYGEN=0 -DLLVM_INCLUDE_DOCS=0 -DLLVM_INCLUDE_TESTS=0
    make -j10 objwriter
    cd ..
    ```

* For ARM(cross/non-cross) please specify Triple for LLVM as Cmake configuration option:
    ```
    -DLLVM_DEFAULT_TARGET_TRIPLE=thumbv7-linux-gnueabi
    ```
* You can change the building type(CMAKE_BUILD_TYPE) to the debugging type(Debug), if necessary to debug ObjWriter.
* Also, you can do this under chroot to building ObjWriter for other platforms.

5. Get ObjWriter:

   If all goes well, the build will complete in the previous step and you will get ObjWriter library as llvm/build/lib/libobjwriter.so
