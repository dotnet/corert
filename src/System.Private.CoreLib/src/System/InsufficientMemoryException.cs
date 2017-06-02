// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
**
** Purpose: The exception class for running out of memory
** but most likely in a non-fatal way that shouldn't 
** be affected by escalation policy.  Use this for cases
** like MemoryFailPoint or a TryAllocate method, where you 
** expect OOM's with no shared state corruption and you
** want to recover from these errors.
**
**
=============================================================================*/

using System;
using System.Runtime.Serialization;

namespace System
{
    public sealed class InsufficientMemoryException : OutOfMemoryException
    {
        // There may be a problem here interacting with the ResourceManager in out of memory conditions,
        // CoreCLR can fall back to literal strings.
        public InsufficientMemoryException()
            : base(SR.Arg_OutOfMemoryException)
        {
            HResult = __HResults.COR_E_INSUFFICIENTMEMORY;
        }

        public InsufficientMemoryException(String message)
            : base(message)
        {
            HResult = __HResults.COR_E_INSUFFICIENTMEMORY;
        }

        public InsufficientMemoryException(String message, Exception innerException)
            : base(message, innerException)
        {
            HResult = __HResults.COR_E_INSUFFICIENTMEMORY;
        }

        internal InsufficientMemoryException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
