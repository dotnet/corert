// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: Wait(), Notify() or NotifyAll() was called from an unsynchronized
**          block of code.
**
**
=============================================================================*/

using System;

namespace System.Threading
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class SynchronizationLockException : Exception
    {
        public SynchronizationLockException()
            : base(SR.Arg_SynchronizationLockException)
        {
            SetErrorCode(__HResults.COR_E_SYNCHRONIZATIONLOCK);
        }

        public SynchronizationLockException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_SYNCHRONIZATIONLOCK);
        }

        public SynchronizationLockException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_SYNCHRONIZATIONLOCK);
        }
    }
}


