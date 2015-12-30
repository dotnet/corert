// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
** Class: SafeArrayRankMismatchException
**
** Purpose: This exception is thrown when the runtime rank of a safe array
**            is different than the array rank specified in the metadata.
**
=============================================================================*/

using System;

namespace System.Runtime.InteropServices
{
    public class SafeArrayRankMismatchException : Exception
    {
        public SafeArrayRankMismatchException()
            : base(SR.Arg_SafeArrayRankMismatchException)
        {
            HResult = __HResults.COR_E_SAFEARRAYRANKMISMATCH;
        }

        public SafeArrayRankMismatchException(String message)
            : base(message)
        {
            HResult = __HResults.COR_E_SAFEARRAYRANKMISMATCH;
        }

        public SafeArrayRankMismatchException(String message, Exception inner)
            : base(message, inner)
        {
            HResult = __HResults.COR_E_SAFEARRAYRANKMISMATCH;
        }
    }
}
