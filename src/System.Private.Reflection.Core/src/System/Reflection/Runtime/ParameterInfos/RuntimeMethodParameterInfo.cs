// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.General;

using Internal.Reflection.Core;

using Internal.Metadata.NativeFormat;

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
                return _typeHandle.Resolve(this.Reader, _typeContext);
            }
        }

        internal sealed override string ParameterTypeString
        {
            get
            {
                return _typeHandle.FormatTypeName(this.Reader, _typeContext);
            }
        }

        protected MetadataReader Reader { get; }


        private readonly Handle _typeHandle;
        private readonly TypeContext _typeContext;
    }
}
