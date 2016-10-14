// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;

using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.CustomAttributes;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

namespace System.Reflection.Runtime.ParameterInfos
{
    // This class is used for the "Get/Set" methods on array types.
    internal sealed partial class RuntimeSyntheticParameterInfo : RuntimeParameterInfo
    {
        private RuntimeSyntheticParameterInfo(MemberInfo memberInfo, int position, RuntimeTypeInfo parameterType)
            : base(memberInfo, position)
        {
            _parameterType = parameterType;
        }

        public sealed override ParameterAttributes Attributes
        {
            get
            {
                return ParameterAttributes.None;
            }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                return Empty<CustomAttributeData>.Enumerable;
            }
        }

        public sealed override Object DefaultValue
        {
            get
            {
                return null; // Legacy: This is what the desktop returns.
            }
        }

        public sealed override bool HasDefaultValue
        {
            get
            {
                return false; // Legacy: Desktop strangely returns true but since we fixed this in Project N for other HasDefaultValues, we'll do so here as well.
            }
        }

        public sealed override String Name
        {
            get
            {
                return null; // Legacy: This is what the dekstop returns.
            }
        }

        public sealed override Type ParameterType
        {
            get
            {
                return _parameterType;
            }
        }

        internal sealed override String ParameterTypeString
        {
            get
            {
                return _parameterType.FormatTypeName();
            }
        }

        private readonly RuntimeTypeInfo _parameterType;
    }
}

