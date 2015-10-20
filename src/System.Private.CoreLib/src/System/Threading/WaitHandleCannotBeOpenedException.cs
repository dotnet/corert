// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// 

// 

//

using System;
using System.Runtime.InteropServices;

namespace System.Threading
{
    [ComVisibleAttribute(false)]

    public class WaitHandleCannotBeOpenedException : Exception
    {
        public WaitHandleCannotBeOpenedException() : base(SR.Threading_WaitHandleCannotBeOpenedException)
        {
            SetErrorCode(__HResults.COR_E_WAITHANDLECANNOTBEOPENED);
        }

        public WaitHandleCannotBeOpenedException(String message) : base(message)
        {
            SetErrorCode(__HResults.COR_E_WAITHANDLECANNOTBEOPENED);
        }

        public WaitHandleCannotBeOpenedException(String message, Exception innerException) : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_WAITHANDLECANNOTBEOPENED);
        }
    }
}

