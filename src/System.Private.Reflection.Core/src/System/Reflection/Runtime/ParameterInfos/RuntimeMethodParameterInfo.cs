// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.General;

using global::Internal.Reflection.Core;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.ParameterInfos
{
    // 
    // Abstract base for all ParameterInfo objects exposed by runtime MethodBase objects
    // (including the ReturnParameter.)
    //
    internal abstract class RuntimeMethodParameterInfo : RuntimeParameterInfo
    {
        protected RuntimeMethodParameterInfo(MethodBase member, int position, MetadataReader reader, Handle typeHandle, TypeContext typeContext)
            : base(member, position)
        {
            Reader = reader;
            _typeHandle = typeHandle;
            _typeContext = typeContext;
        }

        public sealed override Type ParameterType
        {
            get
            {
                return _typeHandle.Resolve(this.Reader, _typeContext).CastToType();
            }
        }

        internal sealed override string ParameterTypeString
        {
            get
            {
                return _typeHandle.FormatTypeName(this.Reader, _typeContext);
            }
        }

        protected MetadataReader Reader { get; private set; }


        private Handle _typeHandle;
        private TypeContext _typeContext;
    }
}
