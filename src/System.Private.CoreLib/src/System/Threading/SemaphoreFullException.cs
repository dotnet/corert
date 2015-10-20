// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// 

// 

using System;
using System.Runtime.InteropServices;

namespace System.Threading
{
    [ComVisibleAttribute(false)]
    [System.Runtime.CompilerServices.TypeForwardedFrom("System, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089")]
    public class SemaphoreFullException : Exception
    {
        public SemaphoreFullException() : base(SR.Threading_SemaphoreFullException)
        {
        }

        public SemaphoreFullException(String message) : base(message)
        {
        }

        public SemaphoreFullException(String message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

