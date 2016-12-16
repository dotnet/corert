// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: Exception to designate an illegal argument to FormatMessage.
**
** 
===========================================================*/

using System;
using System.Runtime.Serialization;

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

        protected FormatException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
