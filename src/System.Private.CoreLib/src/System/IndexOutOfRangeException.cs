// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: Exception class for invalid array indices.
**
**
=============================================================================*/

using System;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class IndexOutOfRangeException : Exception
    {
        public IndexOutOfRangeException()
            : base(SR.Arg_IndexOutOfRangeException)
        {
            SetErrorCode(__HResults.COR_E_INDEXOUTOFRANGE);
        }

        public IndexOutOfRangeException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_INDEXOUTOFRANGE);
        }

        public IndexOutOfRangeException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_INDEXOUTOFRANGE);
        }
    }
}
