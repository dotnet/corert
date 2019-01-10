// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Cts = Internal.TypeSystem;
using Ecma = System.Reflection.Metadata;

namespace ILCompiler.Metadata
{
    /// <summary>
    /// Mixin helper class to allow easy support for ExplicitScopeAttribute for metadata transformation
    /// 
    /// ExplicitScopeAttribute is used to relocate where a given type appears in metadata from one 
    /// metadata assembly to another. It must not be used to relocate into an assembly which otherwise 
    /// exists within the compilation operation. (Current implementation is not reliable in those
    /// circumstances, but there is an assert that in debug builds will detect violations.)
    /// </summary>
    public class ExplicitScopeAssemblyPolicyMixin
    {
        private class AssemblyNameEqualityComparer : IEqualityComparer<AssemblyName>
        {
            public static AssemblyNameEqualityComparer Instance { get; } = new AssemblyNameEqualityComparer();

            public bool ByteArrayCompare(byte[] arr1, byte[] arr2)
            {
                if (arr1.Length != arr2.Length)
                    return false;

                for (int i = 0; i < arr1.Length; i++)
                {
                    if (arr1[i] != arr2[i])
                        return false;
                }
                return true;
            }

            public bool Equals(AssemblyName x, AssemblyName y)
            {
                if (x.Name != y.Name)
                    return false;

                if (x.ContentType != y.ContentType)
                    return false;

                if (x.CultureName != y.CultureName)
                    return false;

                if (x.Flags != y.Flags)
                    return false;
                
                if (x.Flags.HasFlag(AssemblyNameFlags.PublicKey))
                {
                    if (!ByteArrayCompare(x.GetPublicKey(), y.GetPublicKey()))
                        return false;
                }
                else
                {
                    if (!ByteArrayCompare(x.GetPublicKeyToken(), y.GetPublicKeyToken()))
                        return false;
                }

                if (x.ProcessorArchitecture != y.ProcessorArchitecture)
                    return false;

                if (!x.Version.Equals(y.Version))
                    return false;

                return true;
            }

            public int GetHashCode(AssemblyName obj)
            {
                return obj.Name.GetHashCode();
            }
        }

        private class ExplicitScopeAssembly : Cts.ModuleDesc, Cts.IAssemblyDesc
        {
            AssemblyName _assemblyName;

            public override Cts.IAssemblyDesc Assembly => this;

            public ExplicitScopeAssembly(Cts.TypeSystemContext context, AssemblyName assemblyName) : base(context, null)
            {
                _assemblyName = assemblyName;
            }

            AssemblyName Cts.IAssemblyDesc.GetName()
            {
                return _assemblyName;
            }

            public override Cts.MetadataType GetType(string nameSpace, string name, bool throwIfNotFound = true)
            {
                if (throwIfNotFound)
                    throw new TypeLoadException("GetType on an ExplicitScopeAssembly is not supported");
                return null;
            }

            public override Cts.MetadataType GetGlobalModuleType()
            {
                return null;
            }

            public override IEnumerable<Cts.MetadataType> GetAllTypes()
            {
                return Array.Empty<Cts.MetadataType>();
            }
        }

        private Dictionary<AssemblyName, ExplicitScopeAssembly> _dynamicallyGeneratedExplicitScopes = 
            new Dictionary<AssemblyName, ExplicitScopeAssembly>(AssemblyNameEqualityComparer.Instance);

        private Cts.ModuleDesc OverrideModuleOfTypeViaExplicitScope(Cts.MetadataType typeDef)
        {
            if (typeDef.HasCustomAttribute("Internal.Reflection", "ExplicitScopeAttribute"))
            {
                // There is no current cross type system way to represent custom attributes
                Cts.Ecma.EcmaType ecmaType = (Cts.Ecma.EcmaType)typeDef;

                var customAttributeValue = Internal.TypeSystem.Ecma.MetadataExtensions.GetDecodedCustomAttribute(
                    ecmaType, "Internal.Reflection", "ExplicitScopeAttribute");

                if (!customAttributeValue.HasValue)
                    return null;

                if (customAttributeValue.Value.FixedArguments.Length != 1)
                    return null;

                if (customAttributeValue.Value.FixedArguments[0].Type != typeDef.Context.GetWellKnownType(Cts.WellKnownType.String))
                    return null;

                string assemblyNameString = (string)customAttributeValue.Value.FixedArguments[0].Value;
                AssemblyName assemblyName = new AssemblyName(assemblyNameString);
                Debug.Assert(typeDef.Context.ResolveAssembly(assemblyName, false) == null, "ExplicitScopeAttribute must not refer to an assembly which is actually present in the type system.");
                lock(_dynamicallyGeneratedExplicitScopes)
                {
                    ExplicitScopeAssembly explicitScopeAssembly;

                    if (_dynamicallyGeneratedExplicitScopes.TryGetValue(assemblyName, out explicitScopeAssembly))
                    {
                        return explicitScopeAssembly;
                    }
                    explicitScopeAssembly = new ExplicitScopeAssembly(typeDef.Context, assemblyName);
                    _dynamicallyGeneratedExplicitScopes.Add(assemblyName, explicitScopeAssembly);
                    return explicitScopeAssembly;
                }
            }

            return null;
        }

        public Cts.ModuleDesc GetModuleOfType(Cts.MetadataType typeDef)
        {
            Cts.ModuleDesc overrideModule = OverrideModuleOfTypeViaExplicitScope(typeDef);
            if (overrideModule != null)
                return overrideModule;

            return typeDef.Module;
        }
    }
}
