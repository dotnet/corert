// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.General;

using Internal.Reflection.Core;

namespace System.Reflection.Runtime.ParameterInfos
{
    // 
    // Abstract base for all ParameterInfo objects exposed by runtime MethodBase objects
    // (including the ReturnParameter.)
    //
    internal abstract class RuntimeMethodParameterInfo : RuntimeParameterInfo
    {
        protected RuntimeMethodParameterInfo(MethodBase member, int position, QTypeDefRefOrSpec qualifiedParameterTypeHandle, TypeContext typeContext)
            : base(member, position)
        {
            _qualifiedParameterTypeHandle = qualifiedParameterTypeHandle;
            _typeContext = typeContext;
        }

        public sealed override Type ParameterType
        {
            get
            {
                return _lazyParameterType ?? (_lazyParameterType = _qualifiedParameterTypeHandle.Resolve(_typeContext));
            }
        }

        internal sealed override string ParameterTypeString
        {
            get
            {
                return _qualifiedParameterTypeHandle.FormatTypeName(_typeContext);
            }
        }

        protected readonly QTypeDefRefOrSpec _qualifiedParameterTypeHandle;
        private readonly TypeContext _typeContext;
        private volatile Type _lazyParameterType;
    }
}
