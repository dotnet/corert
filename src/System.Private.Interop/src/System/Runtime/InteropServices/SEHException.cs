// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
** Class: SEHException
**
**
** Purpose: Exception class for all Structured Exception Handling code.
**
**
=============================================================================*/

using System;

namespace System.Runtime.InteropServices
{
    public class SEHException : Exception
    {
        public SEHException()
            : base()
        {
            HResult = __HResults.E_FAIL;
        }

        public SEHException(String message)
            : base(message)
        {
            HResult = __HResults.E_FAIL;
        }

        public SEHException(String message, Exception inner)
            : base(message, inner)
        {
            HResult = __HResults.E_FAIL;
        }

        // Exceptions can be resumable, meaning a filtered exception
        // handler can correct the problem that caused the exception,
        // and the code will continue from the point that threw the
        // exception.
        //
        // Resumable exceptions aren't implemented in this version,
        // but this method exists and always returns false.
        //
        public virtual bool CanResume()
        {
            return false;
        }
    }
}
