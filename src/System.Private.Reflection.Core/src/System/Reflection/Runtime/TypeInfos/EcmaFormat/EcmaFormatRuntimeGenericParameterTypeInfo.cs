// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.CustomAttributes;

using Internal.Reflection.Tracing;

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace System.Reflection.Runtime.TypeInfos.EcmaFormat
{
    internal abstract partial class EcmaFormatRuntimeGenericParameterTypeInfo : RuntimeGenericParameterTypeInfo
    {
        protected EcmaFormatRuntimeGenericParameterTypeInfo(MetadataReader reader, GenericParameterHandle genericParameterHandle, GenericParameter genericParameter)
            : base(genericParameter.Index)
        {
            Reader = reader;
            GenericParameterHandle = genericParameterHandle;
            _genericParameter = genericParameter;
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_CustomAttributes(this);
#endif

                return RuntimeCustomAttributeData.GetCustomAttributes(Reader, _genericParameter.GetCustomAttributes());
            }
        }

        public sealed override GenericParameterAttributes GenericParameterAttributes
        {
            get
            {
                return _genericParameter.Attributes;
            }
        }

        public sealed override int MetadataToken
        {
            get
            {
                return MetadataTokens.GetToken(GenericParameterHandle);
            }
        }
        
        protected sealed override int InternalGetHashCode()
        {
            return GenericParameterHandle.GetHashCode();
        }

        protected GenericParameterHandle GenericParameterHandle { get; }

        protected MetadataReader Reader { get; }

        public sealed override string InternalGetNameIfAvailable(ref Type rootCauseForFailure)
        {
            if (_genericParameter.Name.IsNil)
                return string.Empty;
            return _genericParameter.Name.GetString(Reader);
        }

        protected sealed override QTypeDefRefOrSpec[] Constraints
        {
            get
            {
                MetadataReader reader = Reader;
                LowLevelList<QTypeDefRefOrSpec> constraints = new LowLevelList<QTypeDefRefOrSpec>();
                foreach (GenericParameterConstraintHandle constraintHandle in _genericParameter.GetConstraints())
                {
                    GenericParameterConstraint constraint = Reader.GetGenericParameterConstraint(constraintHandle);
                    constraints.Add(new QTypeDefRefOrSpec(reader, constraint.Type));
                }
                return constraints.ToArray();
            }
        }

        private readonly GenericParameter _genericParameter;
    }
}
