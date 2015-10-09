// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: The exception class for versioning problems with DLLS.
**
**
=============================================================================*/

using System;
using System.Diagnostics.Contracts;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class MissingMemberException : MemberAccessException
    {
        public MissingMemberException()
            : base(SR.Arg_MissingMemberException)
        {
            SetErrorCode(__HResults.COR_E_MISSINGMEMBER);
        }

        public MissingMemberException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_MISSINGMEMBER);
        }

        public MissingMemberException(String message, Exception inner)
            : base(message, inner)
        {
            SetErrorCode(__HResults.COR_E_MISSINGMEMBER);
        }

        public override String Message
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get
            { return base.Message; }
        }
    }
}
