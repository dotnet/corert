// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


/*============================================================
**
  Type:  AmbiguousMatchException
**
==============================================================*/

using global::System;

namespace System.Reflection
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AmbiguousMatchException : Exception
    {
        public AmbiguousMatchException()
            : base(SR.RFLCT_Ambiguous)
        {
            HResult = __HResults.COR_E_AMBIGUOUSMATCH;
        }

        public AmbiguousMatchException(String message) : base(message)
        {
            HResult = __HResults.COR_E_AMBIGUOUSMATCH;
        }

        public AmbiguousMatchException(String message, Exception inner) : base(message, inner)
        {
            HResult = __HResults.COR_E_AMBIGUOUSMATCH;
        }
    }
}

