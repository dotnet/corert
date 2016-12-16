// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
