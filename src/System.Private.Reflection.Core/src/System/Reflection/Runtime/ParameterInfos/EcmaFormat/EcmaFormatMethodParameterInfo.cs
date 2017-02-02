// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.CustomAttributes;
using System.Runtime.InteropServices;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace System.Reflection.Runtime.ParameterInfos.EcmaFormat
{
    //
    // This implements ParameterInfo objects owned by MethodBase objects that have an associated Parameter metadata entity.
    //
    internal sealed partial class EcmaFormatMethodParameterInfo : RuntimeMethodParameterInfo
    {
        private EcmaFormatMethodParameterInfo(MethodBase member, MethodDefinitionHandle methodHandle, int position, ParameterHandle parameterHandle, QSignatureTypeHandle qualifiedParameterTypeHandle, TypeContext typeContext)
            : base(member, position, qualifiedParameterTypeHandle, typeContext)
        {
            _methodHandle = methodHandle;
            _parameterHandle = parameterHandle;
            _parameter = Reader.GetParameter(parameterHandle);
        }

        private MetadataReader Reader
        {
            get
            {
                Debug.Assert(QualifiedParameterTypeHandle.Reader is MetadataReader);
                return (MetadataReader)QualifiedParameterTypeHandle.Reader;
            }
        }

        public sealed override ParameterAttributes Attributes
        {
            get
            {
                return _parameter.Attributes;
            }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                IEnumerable<CustomAttributeData> customAttributes = RuntimeCustomAttributeData.GetCustomAttributes(this.Reader, _parameter.GetCustomAttributes());
                foreach (CustomAttributeData cad in customAttributes)
                    yield return cad;

                ParameterAttributes attributes = Attributes;
                if (0 != (attributes & ParameterAttributes.In))
                    yield return ReflectionCoreExecution.ExecutionDomain.GetCustomAttributeData(typeof(InAttribute), null, null);
                if (0 != (attributes & ParameterAttributes.Out))
                    yield return ReflectionCoreExecution.ExecutionDomain.GetCustomAttributeData(typeof(OutAttribute), null, null);
                if (0 != (attributes & ParameterAttributes.Optional))
                    yield return ReflectionCoreExecution.ExecutionDomain.GetCustomAttributeData(typeof(OptionalAttribute), null, null);
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

        public sealed override int MetadataToken
        {
            get
            {
                return MetadataTokens.GetToken(_parameterHandle);
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
                    bool hasDefaultValue = DefaultValueProcessing.GetDefaultValueIfAny(Reader, ref _parameter, this, out defaultValue);

                    if (!hasDefaultValue)
                    {
                        defaultValue = IsOptional ? (object)Missing.Value : (object)DBNull.Value;
                    }
                    defaultValueInfo = _lazyDefaultValueInfo = Tuple.Create(hasDefaultValue, defaultValue);
                }
                return defaultValueInfo;
            }
        }

        private readonly MethodDefinitionHandle _methodHandle;
        private readonly ParameterHandle _parameterHandle;
        private Parameter _parameter;
        private volatile Tuple<bool, Object> _lazyDefaultValueInfo;
    }
}
