// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
** Purpose: The exception class for class loading failures.
**
=============================================================================*/

using System.Runtime.Serialization;

namespace System
{
    public class MissingFieldException : MissingMemberException
    {
        public MissingFieldException()
            : base(SR.Arg_MissingFieldException)
        {
            HResult = __HResults.COR_E_MISSINGFIELD;
        }

        public MissingFieldException(String message)
            : base(message)
        {
            HResult = __HResults.COR_E_MISSINGFIELD;
        }

        public MissingFieldException(String message, Exception inner)
            : base(message, inner)
        {
            HResult = __HResults.COR_E_MISSINGFIELD;
        }

        public MissingFieldException(string className, string methodName)
        {
            ClassName = className;
            MemberName = methodName;
        }

        protected MissingFieldException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
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
