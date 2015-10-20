// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.IO
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class IOException : Exception
    {
        public IOException()
            : base(SR.Arg_IOException)
        {
            SetErrorCode(__HResults.COR_E_IO);
        }

        public IOException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_IO);
        }

        public IOException(String message, int hresult)
            : base(message)
        {
            SetErrorCode(hresult);
        }

        public IOException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_IO);
        }
    }
}
