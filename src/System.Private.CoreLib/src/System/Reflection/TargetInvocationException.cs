// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

using global::System;

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
