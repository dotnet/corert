// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;

using Internal.Reflection.Tracing;

using System.Reflection.Metadata;

namespace System.Reflection.Runtime.TypeInfos.EcmaFormat
{
    internal sealed partial class EcmaFormatRuntimeGenericParameterTypeInfoForTypes : EcmaFormatRuntimeGenericParameterTypeInfo
    {
        private EcmaFormatRuntimeGenericParameterTypeInfoForTypes(MetadataReader reader, GenericParameterHandle genericParameterHandle, RuntimeTypeDefinitionTypeInfo declaringType)
           : base(reader, genericParameterHandle, reader.GetGenericParameter(genericParameterHandle))
        {
            _declaringType = declaringType;
        }

        public sealed override bool IsGenericTypeParameter => true;
        public sealed override bool IsGenericMethodParameter => false;

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
                return _declaringType;
            }
        }

        internal sealed override TypeContext TypeContext
        {
            get
            {
                return _declaringType.TypeContext;
            }
        }

        private readonly RuntimeTypeDefinitionTypeInfo _declaringType;
    }
}

