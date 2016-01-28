// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.General;

using global::Internal.Reflection.Core.NonPortable;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.Types
{
    //
    // This is a RuntimeInspectionOnlyNamedType type that's created "behind the scenes" for types that also have EETypes.
    // The type unification rules dictate that types that have both metadata and EEType be created as RuntimeEENamedTypes
    // as the public-facing "identity." However, if S.R.R. is loaded and metadata is present, the developer naturally expects the
    // Type object to be "fully functional." We accomplish this by creating this shadow type on-demand when
    // a RuntimeEENamedType cannot get what it needs from the raw EEType. In such cases, RuntimeEENamedType delegates
    // calls to this object.
    //
    // ! By necessity, shadow types break the type identity rules - thus, they must NEVER escape out into the wild.
    //
    internal sealed class ShadowRuntimeInspectionOnlyNamedType : RuntimeInspectionOnlyNamedType
    {
        internal ShadowRuntimeInspectionOnlyNamedType(MetadataReader metadataReader, TypeDefinitionHandle typeDefinitionHandle)
            : base(metadataReader, typeDefinitionHandle)
        {
        }


        public sealed override bool InternalViolatesTypeIdentityRules
        {
            get
            {
                return true;
            }
        }
    }
}

