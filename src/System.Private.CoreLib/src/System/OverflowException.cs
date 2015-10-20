// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: Exception class for Arthimatic Overflows.
**
**
=============================================================================*/

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class OverflowException : ArithmeticException
    {
        public OverflowException()
            : base(SR.Arg_OverflowException)
        {
            SetErrorCode(__HResults.COR_E_OVERFLOW);
        }

        public OverflowException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_OVERFLOW);
        }

        public OverflowException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_OVERFLOW);
        }
    }
}
