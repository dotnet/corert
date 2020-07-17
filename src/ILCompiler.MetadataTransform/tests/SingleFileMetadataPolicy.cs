// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using ILCompiler.Metadata;
using Internal.TypeSystem;

namespace MetadataTransformTests
{
    struct SingleFileMetadataPolicy : IMetadataPolicy
    {
        private static object s_lazyInitThreadSafetyLock = new object();

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
            if (typeDef.Name == "IBlockedInterface")
                return true;

            if (typeDef.HasCustomAttribute("System.Runtime.CompilerServices", "__BlockReflectionAttribute"))
                return true;

            return false;
        }

        public bool IsBlocked(MethodDesc method)
        {
            return IsBlocked((MetadataType)method.OwningType);
        }
    }
}
