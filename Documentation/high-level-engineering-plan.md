## Runtime

- Build managed parts
	- Build language source to IL
	- Compile IL assembly with ILC compiler
- Unix
	- Runtime Unix PAL
	- Assembly port
	- Thread suspension - hijacking
- RyuJIT EH
	- Define Runtime/Codegen contract
- Stack unwinding
	- JIT: Produce platform-specific unwind info
	- Runtime to consume the platform specific unwind info
- Managed/native transitions helpers (both pinvoke and reverse pinvoke)
	- JIT: emit the right transition helpers
	- Implement the transition helpers
- GC info encoding (for precise GC)
	- Enable conservative GC
	- Toolchain support to write the GCInfo into final binary
	- Runtime to consume GCInfo produced by RyuJIT today for precise GC

## Toolchain

- Split compilation
	- Design document
		- Robust name mangling
		- Generics (comdat foldable section, or special module)
	- Implementation
		- Phase 1 - Single obj for System.Private.CoreLib
		- Phase 2 - Respective object file for everything else (1:1 Assembly:ObjectFile mapping)
- Produce complete EE types in the toolchain
- Stubs - Delegates, etc.
- Adjustments for RyuJIT / UTC difference

## Reflection

- Produce compact metadata in the final binary
- Produce mapping tables
- Runtime consumption

## Interop

- Move MCG [Marshaling Code Generator](http://blogs.msdn.com/b/dotnet/archive/2014/06/13/net-native-deep-dive-debugging-into-interop-code.aspx) to github
- Package MCG as standalone tool
- Integrate MCG with ILToNative toolchain

## Framework

- Move all .NET Native System.Private* libraries over to github
- Complete .NET Native specific libraries in corefx (build, port to Unix)
- Port to Win32/Unix

## Shared generics

- Toolchain - produce supporting tables and fixups

## CPPCodegen

- Complete IL to CPP codegenerator
- Portable EH
