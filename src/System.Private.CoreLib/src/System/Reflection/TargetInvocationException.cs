// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////
//
// TargetInvocationException is used to report an exception that was thrown
// 

//    by the target of an invocation.
//
// 
// 
//

using System;

namespace System.Reflection
{
    public sealed class TargetInvocationException : Exception
    {
        public TargetInvocationException(System.Exception inner)
            : base(SR.Arg_TargetInvocationException, inner)
        {
            HResult = __HResults.COR_E_TARGETINVOCATION;
        }

        public TargetInvocationException(String message, Exception inner) : base(message, inner)
        {
            HResult = __HResults.COR_E_TARGETINVOCATION;
        }
    }
}
