// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Internal.LightweightInterop
{
    //
    // Lightweight managed wrapper for a COM interface pointer. This is used for the very limited purpose of accessing Dia to generate decent stack traces.
    // Thus, the support is intentionally minimal:
    //
    //   - Each ComIfc wraps a single COM interface pointer and owns one ref-count: there's no attempt to make COM identity match managed object identity.
    //
    internal unsafe abstract class ComInterface
    {
        public const int S_OK = 0;

        protected ComInterface(IntPtr punk)
        {
            Punk = punk;
        }

        public IntPtr Punk { get; private set; } // Unmanaged COM interface pointer. 

        protected IntPtr GetVTableMember(int index)
        {
            unsafe
            {
                IntPtr* pVTable = *((IntPtr**)Punk);
                IntPtr member = pVTable[index];
                return member;
            }
        }

        private void Release()
        {
            IntPtr punk = Punk;
            Punk = (IntPtr)0;
            if (punk != (IntPtr)0)
            {
                IntPtr* pVTable = *((IntPtr**)punk);
                IntPtr releaseMember = pVTable[2];
                S.StdCall<uint>(releaseMember, punk);
            }
        }

        ~ComInterface()
        {
            this.Release();
        }
    }
}
