// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Runtime.CompilerServices
{
    internal static class RuntimeHelpers
    {
        public static int OffsetToStringData
        {
            get
            {
                // Number of bytes from the address pointed to by a reference to
                // a String to the first 16-bit character in the String.  
                // This property allows C#'s fixed statement to work on Strings.
                return String.FIRST_CHAR_OFFSET;
            }
        }

        [Intrinsic]
        public static extern void InitializeArray(Array array, RuntimeFieldHandle fldHandle);

        [Intrinsic]
        internal static extern T UncheckedCast<T>(Object value) where T : class;
    }
}
