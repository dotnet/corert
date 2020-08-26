Rd.xml File Format
==================

The CoreRT ahead of time compiler discovers methods to compile and types to generate by compiling the application entry point and its transitive dependencies. The compiler may miss types if an application uses reflection. For more information about the problem see [Reflection in AOT mode](reflection-in-aot-mode.md).
An rd.xml file can be supplemented to help ILCompiler find types that should be analyzed. This file is similar but more limited than the rd.xml file used by .NET Native.

Minimal Rd.xml configuration

```xml
<Directives xmlns="http://schemas.microsoft.com/netfx/2013/01/metadata">
  <Application>
    <Assembly Name="mscorlib" />
  </Application>
</Directives>
```

ILCompiler supports 2 top level directives `Application` or `Library`. Right now both of them can be used interchangeably and just define area where actual assembly configuration happens.
You can put multiple `<Assembly>` tags inside the `<Application>` directive to configure each assembly individually.

## Assembly directive

There 3 forms how assembly can be configured
- Module metadata only;
- All types;
- Module metadata and selected types.

Module metadata only just need simple `<Assembly>` tag with short name of the assembly.
```xml
<Directives xmlns="http://schemas.microsoft.com/netfx/2013/01/metadata">
  <Application>
    <Assembly Name="mscorlib" />
  </Application>
</Directives>
```

All types in the assembly require adding `Dynamic` attribute with value `Required All`. *NOTE*: This is the only available value for this attribute.
```xml
<Directives xmlns="http://schemas.microsoft.com/netfx/2013/01/metadata">
  <Application>
    <Assembly Name="mscorlib" Dynamic="Required All" />
  </Application>
</Directives>
```
Note that if you have generic types in the assembly, then specific instantiation would not be present in generated code, and if you need one to be included,
then you should include these instantiation using nested `<Type>` tag.

Module metadata and selected types option based on module metadata only mode with added `<Type>` tags inside `<Assembly>`.
```xml
<Directives xmlns="http://schemas.microsoft.com/netfx/2013/01/metadata">
  <Application>
    <Assembly Name="MonoGame.Framework">
      <Type Name="Microsoft.Xna.Framework.Content.ListReader`1[[System.Char,mscorlib]]" Dynamic="Required All" />
    </Assembly>
  </Application>
</Directives>
```

## Types directives.
Type directive provides a way to specify what types are needed. Developer has two options here: 
- Take all type methods;
- Select which methods should be rooted.

Take all type methods:
```xml
<Type Name="Microsoft.Xna.Framework.Content.ListReader`1[[System.Char,mscorlib]]" Dynamic="Required All" />
```

Example how specify typenames
```c#
// just int
System.Int32
// string[]
System.String[]
// string[][]
System.String[][]
// string[,]
System.String[,]
// List<int>
System.Collections.Generic.List`1[[System.Int32,System.Private.CoreLib]]
// Dictionary<int, string>.KeyCollection
System.Collections.Generic.Dictionary`2[[System.Int32,System.Private.CoreLib],[System.String,System.Private.CoreLib]]+KeyCollection
```

Note that it likely does not make sense to have generic type to be placed here, since code generated over specific instantiation of the generic type.
Example of invalid scenario:
```c#
// List<T>
System.Collections.Generic.List`1
```

To select which methods should be rooted add nested `<Method>` tags.
```xml
<Directives xmlns="http://schemas.microsoft.com/netfx/2013/01/metadata">
  <Application>
    <Assembly Name="System.Private.CoreLib">
      <Type Name="System.Collections.Generic.Dictionary`2[[System.Int32,System.Private.CoreLib],[System.String,System.Private.CoreLib]]">
        <Method Name="EnsureCapacity" />
      </Type>
    </Assembly>
  </Application>
</Directives>
```

Alternatively you can specify optional `<Parameter>` tag, if you want only specific overload. For example:
```xml
<Directives xmlns="http://schemas.microsoft.com/netfx/2013/01/metadata">
  <Application>
    <Assembly Name="System.Private.CoreLib">
      <Type Name="System.Collections.Generic.Dictionary`2[[System.Int32,System.Private.CoreLib],[System.String,System.Private.CoreLib]]">
        <Method Name="EnsureCapacity">
          <Parameter Name="System.Int32, System.Private.CoreLib" />
        </Method>
      </Type>
    </Assembly>
  </Application>
</Directives>
```

or if you want instantiate generic method you can pass `<GenericArgument>`.
```xml
<Directives xmlns="http://schemas.microsoft.com/netfx/2013/01/metadata">
  <Application>
    <Assembly Name="System.Private.CoreLib">
      <Type Name="System.Array">
        <Method Name="Empty">
          <GenericArgument Name="System.Int32, System.Private.CoreLib" />
        </Method>
      </Type>
    </Assembly>
  </Application>
</Directives>
```

Take note that methods are distinguished by their method name and parameters. The return value's type is not used in the method signature.
