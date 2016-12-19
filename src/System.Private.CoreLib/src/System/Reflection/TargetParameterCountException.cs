// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
  Type:  TargetParameterCountException
**
==============================================================*/

using System.Runtime.Serialization;

namespace System.Reflection
{
    [Serializable]
    public sealed class TargetParameterCountException : ApplicationException
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

        internal TargetParameterCountException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
