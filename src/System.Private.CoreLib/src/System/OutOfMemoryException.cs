// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: The exception class for OOM.
**
**
=============================================================================*/

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class OutOfMemoryException : Exception
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
    }
}
