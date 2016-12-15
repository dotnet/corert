// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    public sealed class InvalidProgramException : SystemException
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
