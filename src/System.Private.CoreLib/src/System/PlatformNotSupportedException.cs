// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
**
** Purpose: To handle features that don't run on particular platforms
**
**
=============================================================================*/

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class PlatformNotSupportedException : NotSupportedException
    {
        public PlatformNotSupportedException()
            : base(SR.Arg_PlatformNotSupported)
        {
            SetErrorCode(__HResults.COR_E_PLATFORMNOTSUPPORTED);
        }

        public PlatformNotSupportedException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_PLATFORMNOTSUPPORTED);
        }

        public PlatformNotSupportedException(String message, Exception inner)
            : base(message, inner)
        {
            SetErrorCode(__HResults.COR_E_PLATFORMNOTSUPPORTED);
        }
    }
}
