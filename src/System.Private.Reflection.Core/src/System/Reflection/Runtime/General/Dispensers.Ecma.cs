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
        static partial void GetEcmaRuntimeAssembly(AssemblyBindResult bindResult, ref RuntimeAssembly runtimeAssembly)
        {
            if (bindResult.EcmaMetadataReader != null)
                runtimeAssembly = s_EcmaAssemblyDispenser.GetOrAdd(new EcmaRuntimeAssemblyKey(bindResult.EcmaMetadataReader));
        }

        private static readonly Dispenser<EcmaRuntimeAssemblyKey, RuntimeAssembly> s_EcmaAssemblyDispenser =
            DispenserFactory.CreateDispenserV<EcmaRuntimeAssemblyKey, RuntimeAssembly>(
                DispenserScenario.Scope_Assembly,
                delegate (EcmaRuntimeAssemblyKey assemblyDefinition)
                {
                    return (RuntimeAssembly)new EcmaFormat.EcmaFormatRuntimeAssembly(assemblyDefinition.Reader);
                }
        );

        private struct EcmaRuntimeAssemblyKey : IEquatable<EcmaRuntimeAssemblyKey>
        {
            public EcmaRuntimeAssemblyKey(MetadataReader reader)
            {
                Reader = reader;
            }

            public override bool Equals(Object obj)
            {
                if (!(obj is RuntimeAssemblyKey))
                    return false;
                return Equals((RuntimeAssemblyKey)obj);
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
        }
    }
}
