// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
