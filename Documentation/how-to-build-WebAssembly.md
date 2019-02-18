# Build WebAssembly #

## Build WebAssembly on Windows ##

1. Install Emscripten by following the instructions [here](https://kripken.github.io/emscripten-site/docs/getting_started/downloads.html). 
2. Follow the instructions [here](https://kripken.github.io/emscripten-site/docs/getting_started/downloads.html#updating-the-sdk) to update Emscripten to the latest version.
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
4. Build the WebAssembly runtime by running ```./build.sh wasm``` from the repo root.
5. Run the WebAssembly "Hello World" test by opening it in [Firefox](https://www.getfirefox.com).


# How to debug the IL->WebAssembly compilation #
This is Windows only for now.
1. Set the ILCompiler startup command line to ```@C:\corert\tests\src\Simple\HelloWasm\obj\Debug\wasm\native\HelloWasm.ilc.rsp```. That will generate HelloWasm.bc, an LLVM bitcode file.
2. To compile that to WebAssembly, run ```emcc HelloWasm.bc  -s ALLOW_MEMORY_GROWTH=1  C:\corert\bin\WebAssembly.wasm.Debug\sdk\libPortableRuntime.bc C:\corert\bin\WebAssembly.wasm.Debug\sdk\libbootstrappercpp.bc -s WASM=1 -o HelloWasm.html``` (if emcc isn't on your path, you'll need to launch an Emscripten command prompt to do this). That will generate a .wasm file with your code as well as html and js files to run it.

# How to run a WebAssembly application #
1. Ensure you have Edge 41 (Windows only) or above or [Firefox](https://www.getfirefox.com).
2. Open the generated html file in Edge or Firefox and look at the on-screen console for output.

# Useful tips #
* To manually make ILC compile to WebAssembly, add ```--wasm``` to the command line.
* To debug C# source, add ```-g4``` to the emcc command line and change ```-s WASM=1``` to ```-s WASM=0```. This will generate a JavaScript source map that browser debuggers and Visual Studio Code can work with. Using Visual Studio Code's Chrome debugger works particularly well.
* Add ```-g3``` to the emcc command line to generate more debuggable output and a .wast file with the text form of the WebAssembly.
* Change ```-s WASM=1``` to ```-s WASM=0``` in the emcc command line to generate asm.js. Browser debuggers currently work better with asm.js and it's often a bit more readable than wast.
* Add ```-O2 --llvm-lto 2``` to the emcc command line to enable optimizations. This makes the generated WebAssembly as much as 75% smaller as well as more efficient.
