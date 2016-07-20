// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.Types;
using System.Reflection.Runtime.General;


using Internal.Reflection.Tracing;

using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.TypeInfos
{
    internal sealed partial class RuntimeGenericParameterTypeInfoForTypes : RuntimeGenericParameterTypeInfo
    {
        private RuntimeGenericParameterTypeInfoForTypes(MetadataReader reader, GenericParameterHandle genericParameterHandle, RuntimeNamedTypeInfo declaringRuntimeNamedTypeInfo)
           : base(reader, genericParameterHandle)
        {
            DeclaringRuntimeNamedTypeInfo = declaringRuntimeNamedTypeInfo;
        }

        public sealed override MethodBase DeclaringMethod
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_DeclaringMethod(this);
#endif
                return null;
            }
        }

        internal sealed override Type InternalDeclaringType
        {
            get
            {
                return DeclaringRuntimeNamedTypeInfo.AsType();
            }
        }

        internal sealed override TypeContext TypeContext
        {
            get
            {
                return DeclaringRuntimeNamedTypeInfo.TypeContext;
            }
        }

        internal RuntimeNamedTypeInfo DeclaringRuntimeNamedTypeInfo { get; }
    }
}

