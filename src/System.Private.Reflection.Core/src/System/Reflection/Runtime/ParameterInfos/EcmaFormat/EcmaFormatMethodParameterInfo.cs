// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.General.EcmaFormat;
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
    internal sealed partial class EcmaFormatMethodParameterInfo : RuntimeFatMethodParameterInfo
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

        protected sealed override IEnumerable<CustomAttributeData> TrueCustomAttributes => RuntimeCustomAttributeData.GetCustomAttributes(this.Reader, _parameter.GetCustomAttributes());

        protected sealed override bool GetDefaultValueIfAvailable(bool raw, out object defaultValue)
        {
            return DefaultValueProcessing.GetDefaultValueIfAny(Reader, ref _parameter, this, raw, out defaultValue);
        }

        private readonly MethodDefinitionHandle _methodHandle;
        private readonly ParameterHandle _parameterHandle;
        private Parameter _parameter;
    }
}
