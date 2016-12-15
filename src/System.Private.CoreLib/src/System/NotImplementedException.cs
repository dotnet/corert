// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
using System.Runtime.Serialization;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class NotImplementedException : SystemException
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

        protected NotImplementedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
