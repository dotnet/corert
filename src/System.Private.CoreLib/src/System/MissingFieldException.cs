// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
** Purpose: The exception class for class loading failures.
**
=============================================================================*/

using System;

namespace System
{
    public class MissingFieldException : MissingMemberException
    {
        public MissingFieldException()
            : base(SR.Arg_MissingFieldException)
        {
            SetErrorCode(__HResults.COR_E_MISSINGFIELD);
        }

        public MissingFieldException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_MISSINGFIELD);
        }

        public MissingFieldException(String message, Exception inner)
            : base(message, inner)
        {
            SetErrorCode(__HResults.COR_E_MISSINGFIELD);
        }

        public MissingFieldException(string className, string methodName)
        {
            ClassName = className;
            MemberName = methodName;
        }

        public override String Message
        {
            get
            {
                return ClassName == null ? base.Message : SR.Format(SR.MissingField_Name, ClassName + "." + MemberName + (Signature != null ? " " + FormatSignature(Signature) : string.Empty));
            }
        }
    }
}
