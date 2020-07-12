// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Collections.Generic;

using System.Reflection.Runtime.Assemblies;
using System.Reflection.Runtime.Assemblies.EcmaFormat;
using System.Reflection.Runtime.Dispensers;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.PropertyInfos;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeInfos.EcmaFormat;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using System.Reflection.Metadata;

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
        static partial void GetEcmaRuntimeAssembly(AssemblyBindResult bindResult, string assemblyPath, ref RuntimeAssembly runtimeAssembly)
        {
            if (bindResult.EcmaMetadataReader != null)
                runtimeAssembly = EcmaFormatRuntimeAssembly.GetRuntimeAssembly(bindResult.EcmaMetadataReader, assemblyPath);
        }
    }
}

namespace System.Reflection.Runtime.Assemblies.EcmaFormat
{
    internal sealed partial class EcmaFormatRuntimeAssembly
    {
        internal static RuntimeAssembly GetRuntimeAssembly(MetadataReader ecmaMetadataReader, string assemblyPath = null)
        {
            return s_EcmaAssemblyDispenser.GetOrAdd(new EcmaRuntimeAssemblyKey(ecmaMetadataReader, assemblyPath));
        }

        private static readonly Dispenser<EcmaRuntimeAssemblyKey, RuntimeAssembly> s_EcmaAssemblyDispenser =
            DispenserFactory.CreateDispenserV<EcmaRuntimeAssemblyKey, RuntimeAssembly>(
                DispenserScenario.Scope_Assembly,
                delegate (EcmaRuntimeAssemblyKey assemblyDefinition)
                {
                    return (RuntimeAssembly)new EcmaFormatRuntimeAssembly(assemblyDefinition.Reader, assemblyDefinition.AssemblyPath);
                }
        );

        private struct EcmaRuntimeAssemblyKey : IEquatable<EcmaRuntimeAssemblyKey>
        {
            public EcmaRuntimeAssemblyKey(MetadataReader reader, string assemblyPath)
            {
                Reader = reader;
                AssemblyPath = assemblyPath;
            }

            public override bool Equals(Object obj)
            {
                if (!(obj is EcmaRuntimeAssemblyKey other))
                    return false;
                return Equals(other);
            }


            public bool Equals(EcmaRuntimeAssemblyKey other)
            {
                // Equality depends only on the metadata reader of an assembly
                return Object.ReferenceEquals(Reader, other.Reader);
            }

            public override int GetHashCode()
            {
                return Reader.GetHashCode();
            }

            public MetadataReader Reader { get; }
            public string AssemblyPath { get; }
        }
    }
}

namespace System.Reflection.Runtime.FieldInfos.EcmaFormat
{
    //-----------------------------------------------------------------------------------------------------------
    // FieldInfos
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class EcmaFormatRuntimeFieldInfo
    {
        internal static RuntimeFieldInfo GetRuntimeFieldInfo(FieldDefinitionHandle fieldHandle, EcmaFormatRuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo, RuntimeTypeInfo reflectedType)
        {
            return new EcmaFormatRuntimeFieldInfo(fieldHandle, definingTypeInfo, contextTypeInfo, reflectedType).WithDebugName();
        }
    }
}

namespace System.Reflection.Runtime.Modules.EcmaFormat
{
    //-----------------------------------------------------------------------------------------------------------
    // Modules (these exist only because Modules still exist in the Win8P surface area. There is a 1-1
    //          mapping between Assemblies and Modules.)
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class EcmaFormatRuntimeModule
    {
        internal static RuntimeModule GetRuntimeModule(EcmaFormatRuntimeAssembly assembly)
        {
            return new EcmaFormatRuntimeModule(assembly);
        }
    }
}

namespace System.Reflection.Runtime.PropertyInfos.EcmaFormat
{
    //-----------------------------------------------------------------------------------------------------------
    // PropertyInfos
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class EcmaFormatRuntimePropertyInfo
    {
        internal static RuntimePropertyInfo GetRuntimePropertyInfo(PropertyDefinitionHandle propertyHandle, EcmaFormatRuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo, RuntimeTypeInfo reflectedType)
        {
            return new EcmaFormatRuntimePropertyInfo(propertyHandle, definingTypeInfo, contextTypeInfo, reflectedType).WithDebugName();
        }
    }
}

namespace System.Reflection.Runtime.EventInfos.EcmaFormat
{
    //-----------------------------------------------------------------------------------------------------------
    // EventInfos
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class EcmaFormatRuntimeEventInfo
    {
        internal static RuntimeEventInfo GetRuntimeEventInfo(EventDefinitionHandle eventHandle, EcmaFormatRuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo, RuntimeTypeInfo reflectedType)
        {
            return new EcmaFormatRuntimeEventInfo(eventHandle, definingTypeInfo, contextTypeInfo, reflectedType).WithDebugName();
        }
    }
}

namespace System.Reflection.Runtime.ParameterInfos.EcmaFormat
{
    //-----------------------------------------------------------------------------------------------------------
    // ParameterInfos for MethodBase objects with Parameter metadata.
    //-----------------------------------------------------------------------------------------------------------
    internal sealed partial class EcmaFormatMethodParameterInfo
    {
        internal static EcmaFormatMethodParameterInfo GetEcmaFormatMethodParameterInfo(MethodBase member, MethodDefinitionHandle methodHandle, int position, ParameterHandle parameterHandle, QSignatureTypeHandle qualifiedParameterType, TypeContext typeContext)
        {
            return new EcmaFormatMethodParameterInfo(member, methodHandle, position, parameterHandle, qualifiedParameterType, typeContext);
        }
    }
}

namespace System.Reflection.Runtime.CustomAttributes
{
    using EcmaFormat;

    //-----------------------------------------------------------------------------------------------------------
    // CustomAttributeData objects returned by various CustomAttributes properties.
    //-----------------------------------------------------------------------------------------------------------
    internal abstract partial class RuntimeCustomAttributeData
    {
        internal static IEnumerable<CustomAttributeData> GetCustomAttributes(MetadataReader reader, CustomAttributeHandleCollection customAttributeHandles)
        {
            foreach (CustomAttributeHandle customAttributeHandle in customAttributeHandles)
                yield return GetCustomAttributeData(reader, customAttributeHandle);
        }

        public static CustomAttributeData GetCustomAttributeData(MetadataReader reader, CustomAttributeHandle customAttributeHandle)
        {
            return new EcmaFormatCustomAttributeData(reader, customAttributeHandle);
        }
    }
}
