// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


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

