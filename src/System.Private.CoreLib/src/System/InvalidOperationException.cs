// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: Exception class for denoting an object was in a state that
** made calling a method illegal.
**
**
=============================================================================*/

using System;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class InvalidOperationException : Exception
    {
        public InvalidOperationException()
            : base(SR.Arg_InvalidOperationException)
        {
            SetErrorCode(__HResults.COR_E_INVALIDOPERATION);
        }

        public InvalidOperationException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_INVALIDOPERATION);
        }

        public InvalidOperationException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_INVALIDOPERATION);
        }
    }
}

