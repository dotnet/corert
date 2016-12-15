// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
using System.Runtime.Serialization;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class InvalidOperationException : SystemException
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

        protected InvalidOperationException(SerializationInfo info, StreamingContext context) : base(info, context) {}
    }
}

