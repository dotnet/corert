// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ILCompiler.Metadata;
using Internal.TypeSystem;

namespace MetadataTransformTests
{
    /// <summary>
    /// Represents a multifile compilation policy. The list of modules that are to become
    /// part of the metadata blob is passed as an argument to the constructor.
    /// Supports one to one mapping (if modules.Length == 1), or a many to one mapping
    /// (similar to the SharedAssembly concept in .NET Native for UWP).
    /// </summary>
    struct MultifileMetadataPolicy : IMetadataPolicy
    {
        ExplicitScopeAssemblyPolicyMixin _explicitScopePolicyMixin;
        HashSet<ModuleDesc> _modules;

        public MultifileMetadataPolicy(params ModuleDesc[] modules)
        {
            _modules = new HashSet<ModuleDesc>(modules);
            _explicitScopePolicyMixin = new ExplicitScopeAssemblyPolicyMixin();
        }

        public bool GeneratesMetadata(MethodDesc methodDef)
        {
            return GeneratesMetadata((MetadataType)methodDef.OwningType);
        }

        public bool GeneratesMetadata(FieldDesc fieldDef)
        {
            return GeneratesMetadata((MetadataType)fieldDef.OwningType);
        }

        public bool GeneratesMetadata(MetadataType typeDef)
        {
            return _modules.Contains(typeDef.Module);
        }

        public bool IsBlocked(MetadataType typeDef)
        {
            if (typeDef.Name == "ICastable")
                return true;

            if (typeDef.HasCustomAttribute("System.Runtime.CompilerServices", "__BlockReflectionAttribute"))
                return true;

            return false;
        }

        public bool IsBlocked(MethodDesc method)
        {
            return IsBlocked((MetadataType)method.OwningType);
        }

        public ModuleDesc GetModuleOfType(MetadataType typeDef)
        {
            return _explicitScopePolicyMixin.GetModuleOfType(typeDef);
        }
    }
}
