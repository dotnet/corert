// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace System
{
    internal partial class Number
    {
        // WARNING: Don't allocate these on the heap, the "digits" property will return an unmanaged pointer
        // to an interior character array.
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct NumberBuffer
        {
            public Int32 precision;
            public Int32 scale;
            public Boolean sign;

            // Inline array of NumberMaxDigits characters.
            private fixed char buffer[32];

            public char* digits
            {
                get
                {
                    // This is only safe if the caller allocated the NumberBuffer on the stack or pinned it.
                    return ((NumberBuffer*)Unsafe.AsPointer(ref this))->buffer;
                }
            }
        }
    }
}
