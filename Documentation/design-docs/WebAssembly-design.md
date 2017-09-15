# WebAssembly Design

WebAssembly (WASM) is a rapidly evolving platform and significantly different than the ones that .NET already supports. CoreRT is in a good position to target new and different architectures due to its pluggable compiler design and because most of it is written in architecture-independent C#. Many high-level .NET features will hopefully work without modification. The important WASM-specific areas we've considered so far are below.

## Code Generation
For WASM code generation, CoreRT plugs into LLVM's APIs to generate LLVM bitcode. That bitcode can be linked by [Emscripten](http://kripken.github.io/emscripten-site/) into WebAssembly, as well as ASM.js. The reasons for this design are:
1. While it may be possible to create a WASM RyuJIT target architecture, that's likely fairly expensive to get working and even more expensive to build WASM-specific optimizations for. If WASM becomes a major .NET usage scenario, we'd likely revisit this.
2. Emscripten includes a combination of general LLVM optimizations (like eliminating redundant assignments) and WASM-specific optimizations (like controlling the JavaScript optimizer).
3. Compiling with LLVM gives managed code the ability to call into C code, which is valuable for calls into the .NET or C runtimes.
4. An LLVM bitcode generator in CoreRT might be usable for quick ports to future platforms before RyuJIT is ready for them.

### Details
Working via LLVM bitcode implies that some IL concepts need to be translated to C-like concepts. In particular:
1. .NET requires visibility into its stack layout for garbage collection and exception handling. WebAssembly does not allow pointers to the stack at all and LLVM has some limitations on what's available. To get sufficient control over the stack, CoreRT manages its own "shadow" stack that is allocated on the C heap. That also means methods have a custom calling convention. Every method call pushes managed arguments onto the shadow stack, followed by a space for the return value. The method signature fed to LLVM is always the same: void MethodName(int8* ArgumentPointer, int8* ReturnArgumentAddress). This will also allow delegates and reflection to call arbitrary methods without special codegen per-signature.
2. IL includes implicit conversion between various integer and pointer types. For example, the C# code:
```
int b = 0;
fixed(int* a = &b)
{
    int* c = a + 1;
}
```
Translates into IL something like:
```
ldloc.a b // Loads an int&
stloc a // stores into a native int
ldloc a // loads a native int
ldc 1 // loads an int32
add // produces a native int
```
Since those implicit conversions are illegal in C, every load/push operation must convert to canonical types and every store/pop operation must convert to the target type.

3. LLVM's internal representation is strongly typed, but because the C++ rules are different than .NET rules, we only want minimal integration with LLVM's strong typing. To handle that, all .NET types are canonicalized to a small set of LLVM types. Numeric types map directly while structs are mapped to Int8 arrays matching the struct's size. Classes are represented as Int8 pointers.

## Framework Libraries
Many framework libraries should work without modification. However, some APIs (such as files and networking) are platform-dependent. CoreRT and CoreFX include a Platform Abstraction Layer (PAL) built for each supported platform. We will need a WebAssembly PAL that calls into the right C or JavaScript APIs to support functionality. JavaScript sandboxing will limit some operations (such as which network calls are permitted).

## Garbage Collection
The WebAssembly working group is discussing proposals to include garbage collector object reporting, but that's not yet complete and it's not entirely clear if it will work for .NET. While work has not started on enabling the garbage collector, the intent is that the "shadow stack" will provide enough control for stackwalks that perform precise GC reporting.

## Exception Handling
This will need further investigation. The C++ compiler does allow exception handling, although it requires special checks in methods with catch blocks. We may be able to build something similar. The WebAssembly working group has also proposed "zero-cost" exceptions (ones that don't need special checks), but it's not yet clear what that will look like or if it will work for the .NET two-pass exception handling model.

## Interoperability
Interop with C code should work similarly to P/Invokes today with the possible exception of needing to statially link C libraries instead of calling them dynamically. We likely will need to define a JavaScript interop model as well to provide interaction with the rest of the web page.

## Debugging
There is currently very little debugging support for WebAssembly built into browsers. We'll probably need to watch as this expands to understand what's available for CoreRT.

## Threading
WebAssembly does not currently support threading, although there are some proposals under consideration. If WebAssembly does not adopt threading in a timely manner, CoreRT may be able to inject context-switching code into compiled code.
