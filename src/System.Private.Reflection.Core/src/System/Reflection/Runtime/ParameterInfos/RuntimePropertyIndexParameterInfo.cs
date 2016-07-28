// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.PropertyInfos;

using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.ParameterInfos
{
    //
    // This implements ParameterInfo objects returned by PropertyInfo.GetIndexParameters(). Basically, they're identical to the underling accessor method's
    // ParameterInfo's except that the Member property returns the PropertyInfo rather than a MethodBase.
    //
    internal sealed partial class RuntimePropertyIndexParameterInfo : RuntimeParameterInfo
    {
        private RuntimePropertyIndexParameterInfo(RuntimePropertyInfo member, RuntimeParameterInfo backingParameter)
            : base(member, backingParameter.Position)
        {
            _backingParameter = backingParameter;
        }

        public sealed override ParameterAttributes Attributes
        {
            get
            {
                return _backingParameter.Attributes;
            }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                return _backingParameter.CustomAttributes;
            }
        }

        public sealed override Object DefaultValue
        {
            get
            {
                return _backingParameter.DefaultValue;
            }
        }

        public sealed override bool HasDefaultValue
        {
            get
            {
                return _backingParameter.HasDefaultValue;
            }
        }

        public sealed override String Name
        {
            get
            {
                return _backingParameter.Name;
            }
        }

        public sealed override Type ParameterType
        {
            get
            {
                return _backingParameter.ParameterType;
            }
        }

        internal sealed override String ParameterTypeString
        {
            get
            {
                return _backingParameter.ParameterTypeString;
            }
        }

        private readonly RuntimeParameterInfo _backingParameter;
    }
}


