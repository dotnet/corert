// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.IO
{
    public class PathTooLongException : IOException
    {
        public PathTooLongException()
            : base(SR.IO_PathTooLong)
        {
            SetErrorCode(__HResults.COR_E_PATHTOOLONG);
        }

        public PathTooLongException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_PATHTOOLONG);
        }

        public PathTooLongException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_PATHTOOLONG);
        }
    }
}
