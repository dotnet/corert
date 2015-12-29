// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
** Class: SafeArrayTypeMismatchException
**
** Purpose: This exception is thrown when the runtime type of an array
**            is different than the safe array sub type specified in the
**            metadata.
**
=============================================================================*/

using System;

namespace System.Runtime.InteropServices
{
    public class SafeArrayTypeMismatchException : Exception
    {
        public SafeArrayTypeMismatchException()
            : base(SR.Arg_SafeArrayTypeMismatchException)
        {
            HResult = __HResults.COR_E_SAFEARRAYTYPEMISMATCH;
        }

        public SafeArrayTypeMismatchException(String message)
            : base(message)
        {
            HResult = __HResults.COR_E_SAFEARRAYTYPEMISMATCH;
        }

        public SafeArrayTypeMismatchException(String message, Exception inner)
            : base(message, inner)
        {
            HResult = __HResults.COR_E_SAFEARRAYTYPEMISMATCH;
        }
    }
}
