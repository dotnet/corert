// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
** Class: DllNotFoundException
**
**
** Purpose: The exception class for some failed P/Invoke calls.
**
**
=============================================================================*/

namespace System
{
    public class DllNotFoundException : TypeLoadException
    {
        public DllNotFoundException()
            : base(SR.Arg_DllNotFoundException)
        {
            HResult = __HResults.COR_E_DLLNOTFOUND;
        }

        public DllNotFoundException(String message)
            : base(message)
        {
            HResult = __HResults.COR_E_DLLNOTFOUND;
        }

        public DllNotFoundException(String message, Exception inner)
            : base(message, inner)
        {
            HResult = __HResults.COR_E_DLLNOTFOUND;
        }
    }
}
