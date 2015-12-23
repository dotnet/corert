// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.PropertyInfos;

using global::Internal.Metadata.NativeFormat;

using global::Internal.Reflection.Core.NonPortable;

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

        private RuntimeParameterInfo _backingParameter;
    }
}


