// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
** Purpose: The exception class for a misaligned access exception
**
=============================================================================*/

namespace System
{
    public sealed class DataMisalignedException : Exception
    {
        public DataMisalignedException()
            : base(SR.Arg_DataMisalignedException)
        {
            HResult = __HResults.COR_E_DATAMISALIGNED;
        }

        public DataMisalignedException(String message)
            : base(message)
        {
            HResult = __HResults.COR_E_DATAMISALIGNED;
        }

        public DataMisalignedException(String message, Exception innerException)
            : base(message, innerException)
        {
            HResult = __HResults.COR_E_DATAMISALIGNED;
        }
    }
}
