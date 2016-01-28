// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ILCompiler.Metadata;
using Internal.TypeSystem;

namespace MetadataTransformTests
{
    struct SingleFileMetadataPolicy : IMetadataPolicy
    {
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

            return false;
        }
    }
}
