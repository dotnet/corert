// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            get
            {
                return base.Message;
            }
        }
    }
}
