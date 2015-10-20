// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: For methods that should be implemented on subclasses.
**
**
=============================================================================*/

using System;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class NotSupportedException : Exception
    {
        public NotSupportedException()
            : base(SR.Arg_NotSupportedException)
        {
            SetErrorCode(__HResults.COR_E_NOTSUPPORTED);
        }

        public NotSupportedException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_NOTSUPPORTED);
        }

        public NotSupportedException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_NOTSUPPORTED);
        }
    }
}
