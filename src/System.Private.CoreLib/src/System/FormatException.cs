// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
**
**
** Purpose: Exception to designate an illegal argument to FormatMessage.
**
** 
===========================================================*/

using System;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class FormatException : Exception
    {
        public FormatException()
            : base(SR.Arg_FormatException)
        {
            SetErrorCode(__HResults.COR_E_FORMAT);
        }

        public FormatException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_FORMAT);
        }

        public FormatException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_FORMAT);
        }
    }
}
