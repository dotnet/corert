// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
**
** Purpose: The exception class uses to wrap all non-CLS compliant exceptions.
**
**
=============================================================================*/

using System;
using System.Runtime.Serialization;

namespace System.Runtime.CompilerServices
{
    public sealed class RuntimeWrappedException : Exception
    {
        private Object _wrappedException;

        // Not an api but has to be public as System.Linq.Expression invokes this through Reflection when an expression
        // throws an object that doesn't derive from Exception.
        public RuntimeWrappedException(Object thrownObject)
            : base(SR.RuntimeWrappedException)
        {
            HResult = HResults.COR_E_RUNTIMEWRAPPED;
            _wrappedException = thrownObject;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }

        public Object WrappedException
        {
            get { return _wrappedException; }
        }
    }
}
