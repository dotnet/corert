// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
**
** Purpose: The exception class for OOM.
**
**
=============================================================================*/

using System.Runtime.Serialization;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class OutOfMemoryException : SystemException
    {
        public OutOfMemoryException()
            : base(SR.Arg_OutOfMemoryException)
        {
            SetErrorCode(__HResults.COR_E_OUTOFMEMORY);
        }

        public OutOfMemoryException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_OUTOFMEMORY);
        }

        public OutOfMemoryException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_OUTOFMEMORY);
        }

        protected OutOfMemoryException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
