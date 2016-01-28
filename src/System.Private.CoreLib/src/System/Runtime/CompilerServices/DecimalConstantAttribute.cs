// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Note: If you add a new ctor overloads you need to update ParameterInfo.RawDefaultValue

using System.Collections.Generic;

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class DecimalConstantAttribute : Attribute
    {
        [CLSCompliant(false)]
        public DecimalConstantAttribute(
            byte scale,
            byte sign,
            uint hi,
            uint mid,
            uint low
        )
        {
            _dec = new System.Decimal((int)low, (int)mid, (int)hi, (sign != 0), scale);
        }

        public DecimalConstantAttribute(
            byte scale,
            byte sign,
            int hi,
            int mid,
            int low
        )
        {
            _dec = new System.Decimal(low, mid, hi, (sign != 0), scale);
        }

        public System.Decimal Value
        {
            get
            {
                return _dec;
            }
        }

        private System.Decimal _dec;
    }
}

