// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: Exception class for Timeout
**
**
=============================================================================*/

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class TimeoutException : Exception
    {
        public TimeoutException()
            : base(SR.Arg_TimeoutException)
        {
            SetErrorCode(__HResults.COR_E_TIMEOUT);
        }

        public TimeoutException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_TIMEOUT);
        }

        public TimeoutException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_TIMEOUT);
        }
    }
}

