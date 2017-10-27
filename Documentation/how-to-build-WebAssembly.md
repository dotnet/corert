# Build WebAssembly #
Currently, building WebAssembly is only possible on Windows.

1. Install Emscripten by following the instructions [here](https://kripken.github.io/emscripten-site/docs/getting_started/downloads.html). 
2. Follow the instructions [here](https://kripken.github.io/emscripten-site/docs/getting_started/downloads.html#updating-the-sdk) to update Emscripten to the latest version.
3. Get CoreRT set up by following the [Visual Studio instructions](how-to-build-and-run-ilcompiler-in-visual-studio.md).
4. Run the WebAssembly "Hello World" test by running ```C:\corert\tests\runtest.cmd /mode wasm```.

To debug compiling WebAssembly:
1. Set the ILCompiler startup command line to ```@C:\corert\tests\src\Simple\HelloWasm\obj\Debug\x64\native\HelloWasm.ilc.rsp```. That will generate HelloWasm.bc, an LLVM bitcode file.
2. To compile that to WebAssembly, run ```emcc HelloWasm.bc -s WASM=1 -o HelloWasm.html``` (if emcc isn't on your path, you'll need to launch an Emscripten command prompt to do this). That will generate a .wasm file with your code as well as html and js files to run it.

To run a WebAssembly application
1. Ensure you have [Firefox](https://www.getfirefox.com).
2. Open the generated html file in Firefox and look at the on-screen console for output.

Useful tips:
* To manually make ILC compile to WebAssembly, add ```--wasm``` to the command line.
* Add ```-g``` to the emcc command line to generate more debuggable output and a .wast file with the text form of the WebAssembly.
* Omit ```-s WASM=1``` from the emcc command line to generate asm.js. Browser debuggers currently work better with asm.js and it's often a bit more readable than wast.
