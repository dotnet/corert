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
    internal partial class RuntimeAssembly
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
                    AssemblyBindResult bindResult;
                    Exception exception;
                    if (!binder.Bind(convertedAssemblyRefName, out bindResult, out exception))
                        return null;
                    RuntimeAssembly result = null;

                    GetNativeFormatRuntimeAssembly(bindResult, ref result);
                    if (result != null)
                        return result;

                    GetEcmaRuntimeAssembly(bindResult, ref result);
                    if (result != null)
                        return result;

                    return null;
                }
        );

        // Use C# partial method feature to avoid complex #if logic, whichever code files are included will drive behavior
       static partial void GetNativeFormatRuntimeAssembly(AssemblyBindResult bindResult, ref RuntimeAssembly runtimeAssembly);
       static partial void GetEcmaRuntimeAssembly(AssemblyBindResult bindResult, ref RuntimeAssembly runtimeAssembly);
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

namespace System.Reflection.Runtime.MethodInfos
{
    //-----------------------------------------------------------------------------------------------------------
    // ConstructorInfos
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimePlainConstructorInfo<TRuntimeMethodCommon> : RuntimeConstructorInfo
    {
        internal static RuntimePlainConstructorInfo<TRuntimeMethodCommon> GetRuntimePlainConstructorInfo(TRuntimeMethodCommon common)
        {
            return new RuntimePlainConstructorInfo<TRuntimeMethodCommon>(common);
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
    internal sealed partial class RuntimeNamedMethodInfo<TRuntimeMethodCommon>
    {
        internal static RuntimeNamedMethodInfo<TRuntimeMethodCommon> GetRuntimeNamedMethodInfo(TRuntimeMethodCommon common, RuntimeTypeInfo reflectedType)
        {
            RuntimeNamedMethodInfo<TRuntimeMethodCommon> method = new RuntimeNamedMethodInfo<TRuntimeMethodCommon>(common, reflectedType);
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

namespace System.Reflection.Runtime.ParameterInfos
{
    //-----------------------------------------------------------------------------------------------------------
    // ParameterInfos for MethodBase objects with no Parameter metadata.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class RuntimeThinMethodParameterInfo : RuntimeMethodParameterInfo
    {
        internal static RuntimeThinMethodParameterInfo GetRuntimeThinMethodParameterInfo(MethodBase member, int position, QTypeDefRefOrSpec qualifiedParameterType, TypeContext typeContext)
        {
            return new RuntimeThinMethodParameterInfo(member, position, qualifiedParameterType, typeContext);
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
