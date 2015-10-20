// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: For methods that are passed arrays with the wrong number of
**          dimensions.
**
**
=============================================================================*/

using System;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class RankException : Exception
    {
        public RankException()
            : base(SR.Arg_RankException)
        {
            SetErrorCode(__HResults.COR_E_RANK);
        }

        public RankException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_RANK);
        }

        public RankException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_RANK);
        }
    }
}
