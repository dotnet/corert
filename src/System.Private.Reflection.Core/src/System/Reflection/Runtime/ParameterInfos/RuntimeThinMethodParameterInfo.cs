// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.General;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.NonPortable;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.ParameterInfos
{
    //
    // This implements ParameterInfo objects owned by MethodBase objects that have no associated Parameter metadata. (In practice,
    // this means return type "Parameters" that don't have custom attributes.
    //
    internal sealed partial class RuntimeThinMethodParameterInfo : RuntimeMethodParameterInfo
    {
        private RuntimeThinMethodParameterInfo(MethodBase member, int position, ReflectionDomain reflectionDomain, MetadataReader reader, Handle typeHandle, TypeContext typeContext)
            : base(member, position, reflectionDomain, reader, typeHandle, typeContext)
        {
        }

        public sealed override ParameterAttributes Attributes
        {
            get
            {
                return ParameterAttributes.None;
            }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                return Empty<CustomAttributeData>.Enumerable;
            }
        }

        public sealed override Object DefaultValue
        {
            get
            {
                // Returning "null" matches the desktop behavior, though this is inconsistent with the DBNull/Missing values
                // returned by non-return ParameterInfo's without default values. 
                return null;
            }
        }

        public sealed override bool HasDefaultValue
        {
            get
            {
                // COMPAT: Desktop actually returns true here, but that behavior makes no sense.
                return false;
            }
        }

        public sealed override String Name
        {
            get
            {
                return null;
            }
        }
    }
}

