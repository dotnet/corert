// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    public sealed class IndexOutOfRangeException : SystemException
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
