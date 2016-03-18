// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            get
            { return base.Message; }
        }
    }
}
