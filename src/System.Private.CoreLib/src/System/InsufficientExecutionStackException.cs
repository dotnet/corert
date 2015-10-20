// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System
{
    public sealed class InsufficientExecutionStackException : Exception
    {
        public InsufficientExecutionStackException()
            : base(SR.Arg_InsufficientExecutionStackException)
        {
            SetErrorCode(__HResults.COR_E_INSUFFICIENTEXECUTIONSTACK);
        }

        public InsufficientExecutionStackException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_INSUFFICIENTEXECUTIONSTACK);
        }

        public InsufficientExecutionStackException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_INSUFFICIENTEXECUTIONSTACK);
        }
    }
}
