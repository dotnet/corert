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
    // Abstract base for all ParameterInfo objects exposed by runtime MethodBase objects
    // (including the ReturnParameter.)
    //
    internal abstract class RuntimeMethodParameterInfo : RuntimeParameterInfo
    {
        protected RuntimeMethodParameterInfo(MethodBase member, int position, ReflectionDomain reflectionDomain, MetadataReader reader, Handle typeHandle, TypeContext typeContext)
            : base(member, position)
        {
            _reflectionDomain = reflectionDomain;
            Reader = reader;
            _typeHandle = typeHandle;
            _typeContext = typeContext;
        }

        public sealed override Type ParameterType
        {
            get
            {
                return _reflectionDomain.Resolve(this.Reader, _typeHandle, _typeContext);
            }
        }

        internal sealed override string ParameterTypeString
        {
            get
            {
                return _typeHandle.FormatTypeName(this.Reader, _typeContext, _reflectionDomain);
            }
        }

        protected MetadataReader Reader { get; private set; }


        private ReflectionDomain _reflectionDomain;
        private Handle _typeHandle;
        private TypeContext _typeContext;
    }
}
