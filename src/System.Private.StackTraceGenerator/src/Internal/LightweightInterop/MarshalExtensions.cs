// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
