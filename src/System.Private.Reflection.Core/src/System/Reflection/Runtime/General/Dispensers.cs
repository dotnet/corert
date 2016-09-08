// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Collections.Generic;

using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.Assemblies;
using System.Reflection.Runtime.Dispensers;
using System.Reflection.Runtime.PropertyInfos;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Metadata.NativeFormat;

//=================================================================================================================
// This file collects the various chokepoints that create the various Runtime*Info objects. This allows
// easy reviewing of the overall caching and unification policy.
//
// The dispenser functions are defined as static members of the associated Info class. This permits us
// to keep the constructors private to ensure that these really are the only ways to obtain these objects.
//=================================================================================================================

namespace System.Reflection.Runtime.Assemblies
{
    //-----------------------------------------------------------------------------------------------------------
    // Assemblies (maps 1-1 with a MetadataReader/ScopeDefinitionHandle.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeAssembly
    {
        /// <summary>
        /// Returns non-null or throws.
        /// </summary>
        internal static RuntimeAssembly GetRuntimeAssembly(RuntimeAssemblyName assemblyRefName)
        {
            RuntimeAssembly result;
            Exception assemblyLoadException = TryGetRuntimeAssembly(assemblyRefName, out result);
            if (assemblyLoadException != null)
                throw assemblyLoadException;
            return result;
        }

        /// <summary>
        /// Returns null if no assembly matches the assemblyRefName. Throws for other error cases.
        /// </summary>
        internal static RuntimeAssembly GetRuntimeAssemblyIfExists(RuntimeAssemblyName assemblyRefName)
        {
            return s_assemblyRefNameToAssemblyDispenser.GetOrAdd(assemblyRefName);
        }

        internal static Exception TryGetRuntimeAssembly(RuntimeAssemblyName assemblyRefName, out RuntimeAssembly result)
        {
            result = GetRuntimeAssemblyIfExists(assemblyRefName);
            if (result != null)
                return null;
            else
                return new FileNotFoundException(SR.Format(SR.FileNotFound_AssemblyNotFound, assemblyRefName.FullName));
        }

        private static readonly Dispenser<RuntimeAssemblyName, RuntimeAssembly> s_assemblyRefNameToAssemblyDispenser =
            DispenserFactory.CreateDispenser<RuntimeAssemblyName, RuntimeAssembly>(
                DispenserScenario.AssemblyRefName_Assembly,
                delegate (RuntimeAssemblyName assemblyRefName)
                {
                    AssemblyBinder binder = ReflectionCoreExecution.ExecutionDomain.ReflectionDomainSetup.AssemblyBinder;
                    AssemblyName convertedAssemblyRefName = assemblyRefName.ToAssemblyName();
                    MetadataReader reader;
                    ScopeDefinitionHandle scope;
                    Exception exception;
                    IEnumerable<QScopeDefinition> overflowScopes;
                    if (!binder.Bind(convertedAssemblyRefName, out reader, out scope, out overflowScopes, out exception))
                        return null;
                    return GetRuntimeAssembly(reader, scope, overflowScopes);
                }
        );


        private static RuntimeAssembly GetRuntimeAssembly(MetadataReader reader, ScopeDefinitionHandle scope, IEnumerable<QScopeDefinition> overflows)
        {
            return s_scopeToAssemblyDispenser.GetOrAdd(new RuntimeAssemblyKey(reader, scope, overflows));
        }

        private static readonly Dispenser<RuntimeAssemblyKey, RuntimeAssembly> s_scopeToAssemblyDispenser =
            DispenserFactory.CreateDispenserV<RuntimeAssemblyKey, RuntimeAssembly>(
                DispenserScenario.Scope_Assembly,
                delegate (RuntimeAssemblyKey qScopeDefinition)
                {
                    return new RuntimeAssembly(qScopeDefinition.Reader, qScopeDefinition.Handle, qScopeDefinition.Overflows);
                }
        );

        //-----------------------------------------------------------------------------------------------------------
        // Captures a qualified scope (a reader plus a handle) representing the canonical definition of an assembly,
        // plus a set of "overflow" scopes representing additional pieces of the assembly.
        //-----------------------------------------------------------------------------------------------------------
        private struct RuntimeAssemblyKey : IEquatable<RuntimeAssemblyKey>
        {
            public RuntimeAssemblyKey(MetadataReader reader, ScopeDefinitionHandle handle, IEnumerable<QScopeDefinition> overflows)
            {
                _reader = reader;
                _handle = handle;
                _overflows = overflows;
            }

            public MetadataReader Reader { get { return _reader; } }
            public ScopeDefinitionHandle Handle { get { return _handle; } }
            public IEnumerable<QScopeDefinition> Overflows { get { return _overflows; } }
            public ScopeDefinition ScopeDefinition
            {
                get
                {
                    return _handle.GetScopeDefinition(_reader);
                }
            }

            public override bool Equals(Object obj)
            {
                if (!(obj is RuntimeAssemblyKey))
                    return false;
                return Equals((RuntimeAssemblyKey)obj);
            }


            public bool Equals(RuntimeAssemblyKey other)
            {
                // Equality depends only on the canonical definition of an assembly, not
                // the overflows.
                if (!(_reader == other._reader))
                    return false;
                if (!(_handle.Equals(other._handle)))
                    return false;
                return true;
            }

            public override int GetHashCode()
            {
                return _handle.GetHashCode();
            }

            private readonly MetadataReader _reader;
            private readonly ScopeDefinitionHandle _handle;
            private readonly IEnumerable<QScopeDefinition> _overflows;
        }
    }
}

namespace System.Reflection.Runtime.Modules
{
    //-----------------------------------------------------------------------------------------------------------
    // Modules (these exist only because Modules still exist in the Win8P surface area. There is a 1-1
    //          mapping between Assemblies and Modules.)
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeModule
    {
        internal static RuntimeModule GetRuntimeModule(RuntimeAssembly assembly)
        {
            return new RuntimeModule(assembly);
        }
    }
}

namespace System.Reflection.Runtime.FieldInfos
{
    //-----------------------------------------------------------------------------------------------------------
    // FieldInfos
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeFieldInfo
    {
        internal static RuntimeFieldInfo GetRuntimeFieldInfo(FieldHandle fieldHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
        {
            return new RuntimeFieldInfo(fieldHandle, definingTypeInfo, contextTypeInfo).WithDebugName();
        }
    }
}

namespace System.Reflection.Runtime.MethodInfos
{
    //-----------------------------------------------------------------------------------------------------------
    // ConstructorInfos
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimePlainConstructorInfo : RuntimeConstructorInfo
    {
        internal static RuntimePlainConstructorInfo GetRuntimePlainConstructorInfo(MethodHandle methodHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
        {
            return new RuntimePlainConstructorInfo(methodHandle, definingTypeInfo, contextTypeInfo);
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // Constructors for array types.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeSyntheticConstructorInfo : RuntimeConstructorInfo
    {
        internal static RuntimeSyntheticConstructorInfo GetRuntimeSyntheticConstructorInfo(SyntheticMethodId syntheticMethodId, RuntimeTypeInfo declaringType, RuntimeTypeInfo[] runtimeParameterTypes, InvokerOptions options, Func<Object, Object[], Object> invoker)
        {
            return new RuntimeSyntheticConstructorInfo(syntheticMethodId, declaringType, runtimeParameterTypes, options, invoker);
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // MethodInfos for method definitions (i.e. Foo.Moo() or Foo.Moo<>() but not Foo.Moo<int>)
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeNamedMethodInfo : RuntimeMethodInfo
    {
        internal static RuntimeNamedMethodInfo GetRuntimeNamedMethodInfo(MethodHandle methodHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
        {
            RuntimeNamedMethodInfo method = new RuntimeNamedMethodInfo(methodHandle, definingTypeInfo, contextTypeInfo);
            method.WithDebugName();
            return method;
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // MethodInfos for constructed generic methods (Foo.Moo<int> but not Foo.Moo<>)
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeConstructedGenericMethodInfo : RuntimeMethodInfo
    {
        internal static RuntimeMethodInfo GetRuntimeConstructedGenericMethodInfo(RuntimeNamedMethodInfo genericMethodDefinition, RuntimeTypeInfo[] genericTypeArguments)
        {
            return new RuntimeConstructedGenericMethodInfo(genericMethodDefinition, genericTypeArguments).WithDebugName();
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // MethodInfos for the Get/Set methods on array types.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeSyntheticMethodInfo : RuntimeMethodInfo
    {
        internal static RuntimeMethodInfo GetRuntimeSyntheticMethodInfo(SyntheticMethodId syntheticMethodId, String name, RuntimeTypeInfo declaringType, RuntimeTypeInfo[] runtimeParameterTypes, RuntimeTypeInfo returnType, InvokerOptions options, Func<Object, Object[], Object> invoker)
        {
            return new RuntimeSyntheticMethodInfo(syntheticMethodId, name, declaringType, runtimeParameterTypes, returnType, options, invoker).WithDebugName();
        }
    }
}

namespace System.Reflection.Runtime.PropertyInfos
{
    //-----------------------------------------------------------------------------------------------------------
    // PropertyInfos
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimePropertyInfo
    {
        internal static RuntimePropertyInfo GetRuntimePropertyInfo(PropertyHandle propertyHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
        {
            return new RuntimePropertyInfo(propertyHandle, definingTypeInfo, contextTypeInfo).WithDebugName();
        }
    }
}

namespace System.Reflection.Runtime.EventInfos
{
    //-----------------------------------------------------------------------------------------------------------
    // EventInfos
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeEventInfo
    {
        internal static RuntimeEventInfo GetRuntimeEventInfo(EventHandle eventHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
        {
            return new RuntimeEventInfo(eventHandle, definingTypeInfo, contextTypeInfo).WithDebugName();
        }
    }
}

namespace System.Reflection.Runtime.ParameterInfos
{
    //-----------------------------------------------------------------------------------------------------------
    // ParameterInfos for MethodBase objects with no Parameter metadata.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeThinMethodParameterInfo : RuntimeMethodParameterInfo
    {
        internal static RuntimeThinMethodParameterInfo GetRuntimeThinMethodParameterInfo(MethodBase member, int position, MetadataReader reader, Handle typeHandle, TypeContext typeContext)
        {
            return new RuntimeThinMethodParameterInfo(member, position, reader, typeHandle, typeContext);
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // ParameterInfos for MethodBase objects with Parameter metadata.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeFatMethodParameterInfo : RuntimeMethodParameterInfo
    {
        internal static RuntimeFatMethodParameterInfo GetRuntimeFatMethodParameterInfo(MethodBase member, MethodHandle methodHandle, int position, ParameterHandle parameterHandle, MetadataReader reader, Handle typeHandle, TypeContext typeContext)
        {
            return new RuntimeFatMethodParameterInfo(member, methodHandle, position, parameterHandle, reader, typeHandle, typeContext);
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // ParameterInfos returned by PropertyInfo.GetIndexParameters()
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimePropertyIndexParameterInfo : RuntimeParameterInfo
    {
        internal static RuntimePropertyIndexParameterInfo GetRuntimePropertyIndexParameterInfo(RuntimePropertyInfo member, RuntimeParameterInfo backingParameter)
        {
            return new RuntimePropertyIndexParameterInfo(member, backingParameter);
        }
    }

    //-----------------------------------------------------------------------------------------------------------
    // ParameterInfos returned by Get/Set methods on array types.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeSyntheticParameterInfo : RuntimeParameterInfo
    {
        internal static RuntimeSyntheticParameterInfo GetRuntimeSyntheticParameterInfo(MemberInfo member, int position, RuntimeTypeInfo parameterType)
        {
            return new RuntimeSyntheticParameterInfo(member, position, parameterType);
        }
    }
}

namespace System.Reflection.Runtime.CustomAttributes
{
    //-----------------------------------------------------------------------------------------------------------
    // CustomAttributeData objects returned by various CustomAttributes properties.
    //-----------------------------------------------------------------------------------------------------------
    internal abstract partial class RuntimeCustomAttributeData
    {
        internal static IEnumerable<CustomAttributeData> GetCustomAttributes(MetadataReader reader, IEnumerable<CustomAttributeHandle> customAttributeHandles)
        {
            foreach (CustomAttributeHandle customAttributeHandle in customAttributeHandles)
                yield return GetCustomAttributeData(reader, customAttributeHandle);
        }

        private static CustomAttributeData GetCustomAttributeData(MetadataReader reader, CustomAttributeHandle customAttributeHandle)
        {
            return new RuntimeNormalCustomAttributeData(reader, customAttributeHandle);
        }
    }
}
