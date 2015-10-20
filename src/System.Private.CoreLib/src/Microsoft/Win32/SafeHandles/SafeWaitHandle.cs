// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
**
**
** A wrapper for Win32 events (mutexes, auto reset events, and
** manual reset events).  Used by WaitHandle.
**
** 
===========================================================*/

using System;
using System.Security;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using System.Threading;

namespace Microsoft.Win32.SafeHandles
{
    [System.Security.SecurityCritical]  // auto-generated_required
    public sealed class SafeWaitHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        // Called by P/Invoke marshaler
        private SafeWaitHandle() : base(true)
        {
        }

        public SafeWaitHandle(IntPtr existingHandle, bool ownsHandle) : base(ownsHandle)
        {
            SetHandle(existingHandle);
        }

        protected override bool ReleaseHandle()
        {
            Interop.mincore.CloseHandle(handle);
            return true;
        }
    }
}
