// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: The exception class for class loading failures.
**
**
=============================================================================*/

using System;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class MissingMethodException : MissingMemberException
    {
        public MissingMethodException()
            : base(SR.Arg_MissingMethodException)
        {
            SetErrorCode(__HResults.COR_E_MISSINGMETHOD);
        }

        public MissingMethodException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_MISSINGMETHOD);
        }

        public MissingMethodException(String message, Exception inner)
            : base(message, inner)
        {
            SetErrorCode(__HResults.COR_E_MISSINGMETHOD);
        }

        public override String Message
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get
            {
                return base.Message;
            }
        }
    }
}
