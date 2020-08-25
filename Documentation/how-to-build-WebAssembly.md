# Build WebAssembly #

## Build WebAssembly on Windows ##

1. Install Emscripten by following the instructions [here](https://kripken.github.io/emscripten-site/docs/getting_started/downloads.html). 
2. Follow the instructions [here](https://kripken.github.io/emscripten-site/docs/getting_started/downloads.html#updating-the-sdk) to update Emscripten to 2.0.1 ```./emsdk install 2.0.0``` followed by ```./emsdk activate 2.0.0```
3. Install [Firefox](https://www.getfirefox.com) (for testing).
3. Get CoreRT set up by following the [Visual Studio instructions](how-to-build-and-run-ilcompiler-in-visual-studio.md).
4. Build the WebAssembly runtime by running ```build.cmd wasm``` from the repo root.
5. Run the WebAssembly "Hello World" test by running ```C:\corert\tests\runtest.cmd wasm```.

## Build WebAssembly on OSX ##

1. Install Emscripten by following the instructions [here](https://kripken.github.io/emscripten-site/docs/getting_started/downloads.html). 
2. Follow the instructions [here](https://kripken.github.io/emscripten-site/docs/getting_started/downloads.html#updating-the-sdk) to update Emscripten to the latest version.
3. Get CoreRT set up by installing [Prerequisites](prerequisites-for-building.md).
4. Build the WebAssembly runtime by running ```./build.sh wasm``` from the repo root.
5. Run the WebAssembly "Hello World" test by opening it in [Firefox](https://www.getfirefox.com).

## Build WebAssembly on Ubuntu 16.04.3 ##

1. Get CoreRT set up by installing [Prerequisites](prerequisites-for-building.md), except install ```libicu55``` for Ubuntu 16 instead of ```libicu52```.
2. Install Emscripten by following the instructions [here](https://kripken.github.io/emscripten-site/docs/getting_started/downloads.html). 
3. Follow the instructions [here](https://kripken.github.io/emscripten-site/docs/getting_started/downloads.html#updating-the-sdk) to update Emscripten to the latest version.
4. Get a copy of libLLVM.so.  When running ```dotnet restore``` it use a RID of .e.g. ```linux-x64``` .  This is not appropriate for restoring the ```libLLVM.so``` library as the prebuilt libraries in nuget are not generic Linux libraries but are built for specific distros and versions of those distros.  You can force a download of the version for your distro with e.g. for Ubuntu 18.04  ```cd tests/src/Simple/HelloWasm```  and then ``` dotnet restore -v n -r ubuntu.18.04-x64 libLLVMdep.depproj ``` This will download the ```libLLVM.so``` from the libLLVM project https://www.nuget.org/packages/libLLVM/ to ```packages/libllvm.runtime.ubuntu.18.04-x64/9.0.0/runtimes/ubuntu.18.04-x64/native/libLLVM.so``` which can then be copied to ```/usr/lib``` to make it available at runtime.  If there is no version for your distro/version in nuget, then you can either try installing llvm version 9 from your package manager which may or may not contain the shared library, `libLLVM.so`.  If it does not then you can try the downloads for version 9 at https://releases.llvm.org/, and failing that build llvm from source making sure to choose the option to build shared libraries.
5. Build the WebAssembly runtime by running ```./build.sh wasm``` from the repo root.
6. Run the WebAssembly "Hello World" test by opening it in [Firefox](https://www.getfirefox.com).


# How to debug the IL->WebAssembly compilation #
This is Windows only for now.
1. Set the ILCompiler startup command line to ```@C:\corert\tests\src\Simple\HelloWasm\obj\Debug\wasm\native\HelloWasm.ilc.rsp```. That will generate HelloWasm.bc, an LLVM bitcode file.
2. To compile that to WebAssembly, run ```emcc HelloWasm.bc  -s ALLOW_MEMORY_GROWTH=1  C:\corert\bin\WebAssembly.wasm.Debug\sdk\libPortableRuntime.bc C:\corert\bin\WebAssembly.wasm.Debug\sdk\libbootstrappercpp.bc -s WASM=1 -o HelloWasm.html``` (if emcc isn't on your path, you'll need to launch an Emscripten command prompt to do this). That will generate a .wasm file with your code as well as html and js files to run it.

# How to run a WebAssembly application #
1. Ensure you have Edge (Windows only), Chrome or [Firefox](https://www.getfirefox.com).
2. Launch HTTP server which will serve folder with generated html/wasm files. For example `http-server` from npm will serve, but any other option can do too. Without HTTP server, browser will prevent donwloading wasm file due to CORS restrictions.
 *Note*: If you have FireFox, you can disable CORS for local files by going to `about:config`, setting `privacy_file_unique_origin` to `false` and restarting FireFox. Once the changes are applied you will be able to open HTML files directly from the file system without an HTTP server.
3. Open the generated html file in the browser and look at the on-screen console for output.

# Useful tips #
* To manually make ILC compile to WebAssembly, add ```--wasm``` to the command line.
* Add ```-g3``` to the emcc command line to generate more debuggable output and a .wast file with the text form of the WebAssembly.
* Add ```-O2 --llvm-lto 2``` to the emcc command line to enable optimizations. This makes the generated WebAssembly as much as 75% smaller as well as more efficient.
