// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: Exception class for bad arithmetic conditions!
**
**
=============================================================================*/

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class DivideByZeroException : ArithmeticException
    {
        public DivideByZeroException()
            : base(SR.Arg_DivideByZero)
        {
            SetErrorCode(__HResults.COR_E_DIVIDEBYZERO);
        }

        public DivideByZeroException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_DIVIDEBYZERO);
        }

        public DivideByZeroException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_DIVIDEBYZERO);
        }
    }
}
