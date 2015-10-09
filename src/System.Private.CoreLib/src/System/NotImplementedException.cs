// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: Exception thrown when a requested method or operation is not 
**            implemented.
**
**
=============================================================================*/

using System;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class NotImplementedException : Exception
    {
        public NotImplementedException()
            : base(SR.Arg_NotImplementedException)
        {
            SetErrorCode(__HResults.E_NOTIMPL);
        }
        public NotImplementedException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.E_NOTIMPL);
        }
        public NotImplementedException(String message, Exception inner)
            : base(message, inner)
        {
            SetErrorCode(__HResults.E_NOTIMPL);
        }
    }
}
