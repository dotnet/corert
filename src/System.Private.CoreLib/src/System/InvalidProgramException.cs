// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: The exception class for programs with invalid IL or bad metadata.
**
**
=============================================================================*/

using System;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class InvalidProgramException : Exception
    {
        public InvalidProgramException()
            : base(SR.InvalidProgram_Default)
        {
            SetErrorCode(__HResults.COR_E_INVALIDPROGRAM);
        }

        public InvalidProgramException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_INVALIDPROGRAM);
        }

        public InvalidProgramException(String message, Exception inner)
            : base(message, inner)
        {
            SetErrorCode(__HResults.COR_E_INVALIDPROGRAM);
        }
    }
}
