// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

internal partial class Interop
{
    internal unsafe partial class Sys
    {
#if WASM
        [DllImport("*")]
        internal static extern unsafe int rand();
#else
        [DllImport(Interop.Libraries.SystemNative, EntryPoint = "SystemNative_GetNonCryptographicallySecureRandomBytes")]
        internal static extern unsafe void GetNonCryptographicallySecureRandomBytes(byte* buffer, int length);
#endif
    }

    internal static unsafe void GetRandomBytes(byte* buffer, int length)
    {
#if WASM
        // what to do here, handle any size int, assert on sizeof(int) != 4?
        for (int i = 0; i < length / 4; i++)
        {
            int r = Sys.rand();
            *buffer = (byte)(r >> 24);
            buffer++;
            *buffer = (byte)(r >> 16);
            buffer++;
            *buffer = (byte)(r >> 8);
            buffer++;
            *buffer = (byte)r;
            buffer++;
        }
        int remaining = length % 4;
        if(remaining > 0)
        {
            int r = Sys.rand();
            if(remaining >= 3)
            {
                *buffer = (byte)(r >> 24);
                buffer++;
            }
            if(remaining >= 2)
            {
                *buffer = (byte)(r >> 16);
                buffer++;
            }
            *buffer = (byte)(r >> 8);
            buffer++;
        }
#else
        Sys.GetNonCryptographicallySecureRandomBytes(buffer, length);
#endif
    }
}
