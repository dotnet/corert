// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.CustomAttributes;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.ParameterInfos
{
    //
    // This implements ParameterInfo objects owned by MethodBase objects that have an associated Parameter metadata entity.
    //
    internal sealed partial class RuntimeFatMethodParameterInfo : RuntimeMethodParameterInfo
    {
        private RuntimeFatMethodParameterInfo(MethodBase member, MethodHandle methodHandle, int position, ParameterHandle parameterHandle, MetadataReader reader, Handle typeHandle, TypeContext typeContext)
            : base(member, position, reader, typeHandle, typeContext)
        {
            _methodHandle = methodHandle;
            _parameterHandle = parameterHandle;
            _parameter = parameterHandle.GetParameter(reader);
        }

        public sealed override ParameterAttributes Attributes
        {
            get
            {
                return _parameter.Flags;
            }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                IEnumerable<CustomAttributeData> customAttributes = RuntimeCustomAttributeData.GetCustomAttributes(this.Reader, _parameter.CustomAttributes);
                foreach (CustomAttributeData cad in customAttributes)
                    yield return cad;
                MethodHandle declaringMethodHandle = _methodHandle;
                foreach (CustomAttributeData cad in ReflectionCoreExecution.ExecutionEnvironment.GetPsuedoCustomAttributes(this.Reader, _parameterHandle, declaringMethodHandle))
                    yield return cad;
            }
        }

        public sealed override Object DefaultValue
        {
            get
            {
                return DefaultValueInfo.Item2;
            }
        }

        public sealed override bool HasDefaultValue
        {
            get
            {
                return DefaultValueInfo.Item1;
            }
        }

        public sealed override String Name
        {
            get
            {
                return _parameter.Name.GetStringOrNull(this.Reader);
            }
        }

        private Tuple<bool, Object> DefaultValueInfo
        {
            get
            {
                Tuple<bool, Object> defaultValueInfo = _lazyDefaultValueInfo;
                if (defaultValueInfo == null)
                {
                    Object defaultValue;
                    bool hasDefaultValue = ReflectionCoreExecution.ExecutionEnvironment.GetDefaultValueIfAny(
                        this.Reader,
                        _parameterHandle,
                        this.ParameterType,
                        this.CustomAttributes,
                        out defaultValue);
                    if (!hasDefaultValue)
                    {
                        defaultValue = IsOptional ? (object)Missing.Value : (object)DBNull.Value;
                    }
                    defaultValueInfo = _lazyDefaultValueInfo = Tuple.Create(hasDefaultValue, defaultValue);
                }
                return defaultValueInfo;
            }
        }

        private readonly MethodHandle _methodHandle;
        private readonly ParameterHandle _parameterHandle;
        private readonly Parameter _parameter;
        private volatile Tuple<bool, Object> _lazyDefaultValueInfo;
    }
}
