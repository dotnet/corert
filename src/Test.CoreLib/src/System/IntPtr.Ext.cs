// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace System
{
    partial struct IntPtr
    {
        [Intrinsic]
        public unsafe long ToInt64()
        {
#if BIT64
            return (long)_value;
#else
            return (long)(int)_value;
#endif
        }
    }
}
