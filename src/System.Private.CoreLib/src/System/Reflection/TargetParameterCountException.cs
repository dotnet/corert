// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
  Type:  TargetParameterCountException
**
==============================================================*/

using global::System;

namespace System.Reflection
{
    public sealed class TargetParameterCountException : Exception
    {
        public TargetParameterCountException()
            : base(SR.Arg_TargetParameterCountException)
        {
            HResult = __HResults.COR_E_TARGETPARAMCOUNT;
        }

        public TargetParameterCountException(String message)
            : base(message)
        {
            HResult = __HResults.COR_E_TARGETPARAMCOUNT;
        }

        public TargetParameterCountException(String message, Exception inner)
            : base(message, inner)
        {
            HResult = __HResults.COR_E_TARGETPARAMCOUNT;
        }
    }
}

