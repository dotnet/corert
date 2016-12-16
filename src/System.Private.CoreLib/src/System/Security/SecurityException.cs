// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Security
{
    public class SecurityException : Exception
    {
        public SecurityException()
            : base(SR.Arg_SecurityException)
        {
            SetErrorCode(System.__HResults.COR_E_SECURITY);
        }

        public SecurityException(String message)
            : base(message)
        {
            SetErrorCode(System.__HResults.COR_E_SECURITY);
        }

        public SecurityException(String message, Exception inner)
            : base(message, inner)
        {
            SetErrorCode(System.__HResults.COR_E_SECURITY);
        }

        public override String ToString()
        {
            return base.ToString();
        }
    }
}
