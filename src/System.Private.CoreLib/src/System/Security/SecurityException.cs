// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Security
{
    [System.Runtime.InteropServices.ComVisible(true)]
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
