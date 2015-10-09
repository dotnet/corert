// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Security
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class VerificationException : Exception
    {
        public VerificationException()
            : base(SR.Verification_Exception)
        {
            SetErrorCode(__HResults.COR_E_VERIFICATION);
        }

        public VerificationException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_VERIFICATION);
        }

        public VerificationException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_VERIFICATION);
        }
    }
}
