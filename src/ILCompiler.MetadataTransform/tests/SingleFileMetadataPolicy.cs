// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using ILCompiler.Metadata;
using Internal.TypeSystem;

namespace MetadataTransformTests
{
    struct SingleFileMetadataPolicy : IMetadataPolicy
    {
        private static object s_lazyInitThreadSafetyLock = new object();
        private ExplicitScopeAssemblyPolicyMixin _explicitScopePolicyMixin;

        public bool GeneratesMetadata(MethodDesc methodDef)
        {
            return true;
        }

        public bool GeneratesMetadata(FieldDesc fieldDef)
        {
            return true;
        }

        public bool GeneratesMetadata(MetadataType typeDef)
        {
            return true;
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
            if (_explicitScopePolicyMixin == null)
            {
                lock (s_lazyInitThreadSafetyLock)
                {
                    if (_explicitScopePolicyMixin == null)
                        _explicitScopePolicyMixin = new ExplicitScopeAssemblyPolicyMixin();
                }
            }

            return _explicitScopePolicyMixin.GetModuleOfType(typeDef);
        }
    }
}
