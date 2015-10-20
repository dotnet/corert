// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
** Class:  SafeFileHandle 
**
**
** A wrapper for file handles
**
** 
===========================================================*/

using System;
using Microsoft.Win32;

namespace Microsoft.Win32.SafeHandles
{
    [System.Security.SecurityCritical]
    internal sealed class SafeFileHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeFileHandle() : base(true)
        {
        }

        public SafeFileHandle(IntPtr preexistingHandle, bool ownsHandle)
            : base(ownsHandle)
        {
            SetHandle(preexistingHandle);
        }

        [System.Security.SecurityCritical]
        override protected bool ReleaseHandle()
        {
            return Interop.mincore.CloseHandle(DangerousGetHandle());
        }
    }
}

