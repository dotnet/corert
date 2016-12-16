// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

