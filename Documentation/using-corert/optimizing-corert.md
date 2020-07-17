# Optimizing programs targeting CoreRT

The CoreRT compiler provides multiple switches to influence the compilation process. These switches control the code and metadata that the compiler generates and affect the runtime behavior of the compiled program.

To specify a switch, add a new property to your project file with one or more of the values below. For example, to specify the invariant globalization mode, add

```xml
  <PropertyGroup>
    <IlcInvariantGlobalization>true</IlcInvariantGlobalization>
  </PropertyGroup>
```

under the `<Project>` node of your project file.

## Options related to globalization
* `<IlcInvariantGlobalization>true</IlcInvariantGlobalization>`: enables the [globalization invariant mode](https://github.com/dotnet/runtime/blob/master/docs/design/features/globalization-invariant-mode.md) that removes code and data that supports non-english cultures. Removing code and data makes your app smaller.

## Options related to reflection

By default, the compiler tries to maximize compatibility with existing .NET code at the expense of compilation speed and size of the output executable. This allows people to use their existing code that worked well in a fully dynamic mode without hitting issues caused by full AOT compilation. To read more about reflection, see the [Reflection in AOT mode](reflection-in-aot-mode.md) document. The compatibility behaviors can be turned off by adding/editing following properties in your project file:

* `<RootAllApplicationAssemblies>false</RootAllApplicationAssemblies>`: this disables the compiler behavior where all code in the application assemblies is considered dynamically reachable. Leaving this option at the default value (`true`) has a significant effect on the size of the resulting executable because it prevents removal of unused code that would otherwise happen. The default value ensures compatibility with reflection-heavy applications.
* `<IlcGenerateCompleteTypeMetadata>false</IlcGenerateCompleteTypeMetadata>`: this disables generation of complete type metadata. This is a compilation mode that prevents a situation where some members of a type are visible to reflection at runtime, but others aren't, because they weren't compiled.
* `<IlcGenerateStackTraceData>false</IlcGenerateStackTraceData>`: this disables generation of stack trace metadata that provides textual names in stack traces. This is for example the text string one gets by calling `Exception.ToString()` on a caught exception. With this option disabled, stack traces will still be generated, but will be based on reflection metadata alone (they might be less complete).
* `<IlcDisableReflection>true</IlcDisableReflection>`: this completely disables the reflection metadata generation. Very basic reflection will still work (you can still use `typeof`, call `Object.GetType()`, compare the results, and query for basic properties such as `Type.IsValueType` or `Type.BaseType`), but most of the reflection stack will no longer work (no way to query/access methods and fields on types, or get names of types). This mode is experimental - more details in the [Reflection free mode](reflection-free-mode.md) document.

## Options related to code generation
* `<IlcOptimizationPreference>Speed</IlcOptimizationPreference>`: when generating optimized code, favor code execution speed.
* `<IlcOptimizationPreference>Size</IlcOptimizationPreference>`: when generating optimized code, favor smaller code size.
* `<IlcFoldIdenticalMethodBodies>true</IlcFoldIdenticalMethodBodies>`: folds method bodies with identical bytes (method body deduplication). This makes your app smaller, but the stack traces might sometimes look nonsensical (unexpected methods might show up in the stack trace because the expected method had the same bytes as the unexpected method). Note: the current implementation of deduplication doesn't attempt to make the folding unobservable to managed code: delegates pointing to two logically different methods that ended up being folded together will compare equal.

## Special considerations for Linux/macOS

Debugging symbols (data about your program required for debugging) is by default part of native executable files on Unix-like operating systems. To minimize the size of your CoreRT-compiled executable, you can run the `strip` tool to remove the debugging symbols.

No action is needed on Windows since the platform convention is to generate debug information into a separate file (`*.pdb`).

## Advanced options 
* `<IlcDisableUnhandledExceptionExperience>true</IlcDisableUnhandledExceptionExperience>`: disables code that prints stack traces for unhandled exceptions to the console.
* `<IlcSystemModule>classlibmodule</IlcSystemModule>`: Name of the module which contains basic classes. When specified, disable automatic referencing of the `System.Private.CoreLib` and other libraries. See https://github.com/MichalStrehovsky/zerosharp for example of usage.
