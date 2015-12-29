// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
** Class: MarshalDirectiveException
**
** Purpose: This exception is thrown when the marshaller encounters a signature
**          that has an invalid MarshalAs CA for a given argument or is not
**          supported.
**
=============================================================================*/

using System;

namespace System.Runtime.InteropServices
{
    public class MarshalDirectiveException : Exception
    {
        public MarshalDirectiveException()
            : base(SR.Arg_MarshalDirectiveException)
        {
            HResult = __HResults.COR_E_MARSHALDIRECTIVE;
        }

        public MarshalDirectiveException(String message)
            : base(message)
        {
            HResult = __HResults.COR_E_MARSHALDIRECTIVE;
        }

        public MarshalDirectiveException(String message, Exception inner)
            : base(message, inner)
        {
            HResult = __HResults.COR_E_MARSHALDIRECTIVE;
        }
    }
}
