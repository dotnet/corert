// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//

//

using System;

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class LCIDConversionAttribute : Attribute
    {
        internal int _val;
        public LCIDConversionAttribute(int lcid)
        {
            _val = lcid;
        }
        public int Value { get { return _val; } }
    }
}
