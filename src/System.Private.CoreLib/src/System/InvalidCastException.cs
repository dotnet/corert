// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
** Purpose: Exception class for invalid cast conditions!
**
=============================================================================*/

using System;

namespace System
{
    public class InvalidCastException : Exception
    {
        public InvalidCastException()
            : base(SR.Arg_InvalidCastException)
        {
            SetErrorCode(__HResults.COR_E_INVALIDCAST);
        }

        public InvalidCastException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_INVALIDCAST);
        }

        public InvalidCastException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_INVALIDCAST);
        }

        public InvalidCastException(String message, int errorCode)
            : base(message)
        {
            SetErrorCode(errorCode);
        }
    }
}
