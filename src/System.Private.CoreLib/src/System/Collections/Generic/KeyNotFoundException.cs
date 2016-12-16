// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Collections.Generic
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class KeyNotFoundException : Exception
    {
        public KeyNotFoundException()
            : base(SR.Arg_KeyNotFound)
        {
            SetErrorCode(System.__HResults.COR_E_KEYNOTFOUND);
        }

        public KeyNotFoundException(String message)
            : base(message)
        {
            SetErrorCode(System.__HResults.COR_E_KEYNOTFOUND);
        }

        public KeyNotFoundException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(System.__HResults.COR_E_KEYNOTFOUND);
        }
    }
}
