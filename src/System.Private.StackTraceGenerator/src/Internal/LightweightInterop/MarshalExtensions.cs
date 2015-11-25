// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Diagnostics;
using global::System.Runtime.InteropServices;

namespace Internal.LightweightInterop
{
    internal static class MarshalExtensions
    {
        public static String MarshalBstr(this IntPtr bstr)
        {
            unsafe
            {
                if (bstr == ((IntPtr)0))
                    return null;
                try
                {
                    char* pc = (char*)bstr;
                    int cchLen = 0;
                    // This marshaler is for stack traces so if a string looks suspiciously long, chop it.
                    while (pc[cchLen] != '\0' && cchLen < 300)
                    {
                        cchLen++;
                    }
                    return new String(pc, 0, cchLen);
                }
                finally
                {
                    SysFreeString(bstr);
                }
            }
        }

        [DllImport("OleAut32")]
        private static extern void SysFreeString(IntPtr bstr);
    }
}
